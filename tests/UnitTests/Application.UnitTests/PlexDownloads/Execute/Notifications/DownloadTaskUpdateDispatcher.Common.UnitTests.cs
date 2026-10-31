namespace Reaparr.Application.UnitTests;

public class DownloadTaskUpdateDispatcherCommonUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    [Test]
    public async Task ShouldNotSendPatch_WhenProgressIsUpdatedForUnknownKey()
    {
        var capturedPatches = new List<IReadOnlyCollection<DownloadPatchDTO>>();
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, upserts, _, _) => capturedPatches.Add(upserts)
            )
            .Returns(Task.CompletedTask);

        var sut = Sut;
        sut.OnProgressUpdated(
            new DownloadTaskKey
            {
                Id = Guid.NewGuid(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Type = DownloadTaskType.EpisodeData,
            },
            new DownloadTaskProgress
            {
                DataTotal = 10,
                DataReceived = 5,
                Percentage = 50,
                DownloadSpeed = 1,
            }
        );

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(1500, CancellationToken);
        await sut.StopAsync(CancellationToken.None);

        capturedPatches.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldPersistTerminalProgressAfterBufferedEstimate_WhenFinalProgressArrives()
    {
        // Arrange
        await SetupDatabase(
            84332,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = downloadTask.ToKey();
        var capturedPatches = new List<IReadOnlyCollection<DownloadPatchDTO>>();
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, upserts, _, _) => capturedPatches.Add(upserts)
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());

        var finalProgress = new DownloadTaskProgress
        {
            DataTotal = 4_321,
            DataReceived = 4_321,
            Percentage = 100m,
            DownloadSpeed = 0,
            TimeRemaining = 0,
        };
        var sut = Sut;
        await sut.StartAsync(CancellationToken.None);
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 999,
                DataReceived = 500,
                Percentage = 50m,
                DownloadSpeed = 100,
                TimeRemaining = 12,
            }
        );

        // Act
        var result = await sut.PersistProgressAsync(key, finalProgress, cancellationToken: CancellationToken.None);
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 111,
                DataReceived = 100,
                Percentage = 1m,
                DownloadSpeed = 2,
                TimeRemaining = 30,
            }
        );
        await sut.StopAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var persisted = await IDbContext.GetDownloadTaskFileAsync(key, CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.DataTotal.ShouldBe(4_321);
        persisted.DataReceived.ShouldBe(4_321);
        persisted.Percentage.ShouldBe(100m);
        persisted.DownloadSpeed.ShouldBe(0);
        persisted.TimeRemaining.ShouldBe(0);
        capturedPatches
            .SelectMany(x => x)
            .ShouldContain(x =>
                x.Id == key.Id && x.DataTotal == 4_321 && x.DataReceived == 4_321 && x.Percentage == 100m
            );
        Mock.Mock<IDownloadHubService>().Verify();
    }

    [Test]
    public async Task ShouldPublishTransferProgressAfterTerminalProgress_WhenLateDownloadEstimateArrives()
    {
        // Arrange
        await SetupDatabase(
            84333,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = downloadTask.ToKey();
        var capturedPatches = new List<IReadOnlyCollection<DownloadPatchDTO>>();
        var transferPatchEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifyRequested = 0;
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, upserts, _, _) =>
                {
                    capturedPatches.Add(upserts);
                    if (
                        Volatile.Read(ref notifyRequested) != 0
                        && upserts.Any(x =>
                            x.Id == key.Id && x.DataTotal == 4_321 && x.DataReceived == 4_321 && x.Percentage == 100m
                        )
                    )
                        transferPatchEntered.TrySetResult();
                }
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());

        var sut = Sut;
        await sut.StartAsync(CancellationToken.None);
        var finalProgress = new DownloadTaskProgress
        {
            DataTotal = 4_321,
            DataReceived = 4_321,
            Percentage = 100m,
            DownloadSpeed = 0,
            TimeRemaining = 0,
        };

        // Act
        var result = await sut.PersistProgressAsync(key, finalProgress, cancellationToken: CancellationToken.None);
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 999,
                DataReceived = 500,
                Percentage = 50m,
                DownloadSpeed = 100,
                TimeRemaining = 12,
            }
        );
        Volatile.Write(ref notifyRequested, 1);
        sut.NotifyFileTransferProgress(key);
        await transferPatchEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var persisted = await IDbContext.GetDownloadTaskFileAsync(key, CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.DataTotal.ShouldBe(4_321);
        persisted.DataReceived.ShouldBe(4_321);
        persisted.Percentage.ShouldBe(100m);
        capturedPatches
            .SelectMany(x => x)
            .ShouldContain(x =>
                x.Id == key.Id && x.DataTotal == 4_321 && x.DataReceived == 4_321 && x.Percentage == 100m
            );
        Mock.Mock<IDownloadHubService>().Verify();
    }
}
