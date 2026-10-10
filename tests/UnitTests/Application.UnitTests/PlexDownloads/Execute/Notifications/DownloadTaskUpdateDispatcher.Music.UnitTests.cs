namespace Reaparr.Application.UnitTests;

public class DownloadTaskUpdateDispatcherMusicUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    [Test]
    [Arguments(DownloadStatus.Downloading, false)]
    [Arguments(DownloadStatus.MovePaused, false)]
    [Arguments(DownloadStatus.Queued, true)]
    [Arguments(DownloadStatus.Paused, true)]
    public async Task ShouldPersistMusicStateAndPatchOnlyTrackAndAncestors_WhenUpdatesAreBuffered(
        DownloadStatus status,
        bool withProgress
    )
    {
        // Arrange
        await SetupDatabase(
            88003,
            c =>
            {
                c.PlexMusicLibraryCount = 1;
                c.MusicArtistDownloadTasksCount = 2;
                c.MusicAlbumDownloadTasksCount = 1;
                c.MusicTrackDownloadTasksCount = 1;
                c.MusicTrackFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var files = await dbContext.DownloadTaskMusicTrackFiles
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        var target = files[0];
        var sibling = files[1];
        var track = await dbContext.DownloadTaskMusicTracks.SingleAsync(
            x => x.Id == target.ParentId,
            CancellationToken
        );
        var album = await dbContext.DownloadTaskMusicAlbums.SingleAsync(x => x.Id == track.ParentId, CancellationToken);
        var ancestorIds = new[] { track.Id, album.Id, album.ParentId };
        await dbContext
            .DownloadTaskMusicTrackFiles.Where(x => x.Id == sibling.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.ParentId, target.ParentId), CancellationToken);
        await dbContext.DownloadTaskMusicTrackFiles.ExecuteUpdateAsync(
            p => p.SetProperty(x => x.DataTotal, 1000),
            CancellationToken
        );
        var key = target.ToKey();

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(target.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());
        var expectedIds = ancestorIds.Append(target.Id).Order().ToArray();
        (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.Queued
        );
        (await dbContext.GetDownloadTaskFileAsync(sibling.ToKey(), CancellationToken))!.DataReceived.ShouldBe(0);
        if (status == DownloadStatus.MovePaused)
        {
            await dbContext
                .DownloadTaskMusicTrackFiles.Where(x => x.Id == target.Id)
                .ExecuteUpdateAsync(
                    p => p.SetProperty(x => x.FileDataTransferred, 250).SetProperty(x => x.Percentage, 25),
                    CancellationToken
                );
        }

        var snapshot = new DirectDownloadSnapshot
        {
            SaveProgress = 50,
            Status = 2,
            Urls = ["https://plex/target"],
            TotalFileSize = 1000,
            FileName = target.FileName,
            DownloadingFileExtension = ".reaptemp",
            IsSupportDownloadInRange = true,
            Chunks =
            [
                new DirectDownloadSnapshotChunk
                {
                    Id = "target-chunk",
                    Start = 0,
                    End = 999,
                    Position = 500,
                    MaxTryAgainOnFailure = 3,
                    Timeout = 1000,
                },
            ],
        };
        var received = new System.Collections.Concurrent.ConcurrentQueue<(long Sequence, DownloadPatchDTO[] Patches)>();
        var patchReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    target.PlexServerId,
                    It.IsAny<long>(),
                    It.Is<IReadOnlyCollection<DownloadPatchDTO>>(p =>
                        p.Select(n => n.Id).Order().SequenceEqual(expectedIds)
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, sequence, patches, _, _) =>
                {
                    received.Enqueue((sequence, patches.ToArray()));
                    patchReceived.TrySetResult(true);
                }
            )
            .Returns(Task.CompletedTask);
        var sut = Sut;

        // Act
        if (withProgress)
        {
            sut.OnProgressUpdated(
                key,
                new DownloadTaskProgress
                {
                    DataTotal = 1000,
                    DataReceived = 100,
                    Percentage = 10,
                    DownloadSpeed = 20,
                    TimeRemaining = 45,
                },
                snapshot with
                {
                    SaveProgress = 10,
                }
            );
            sut.OnProgressUpdated(
                key,
                new DownloadTaskProgress
                {
                    DataTotal = 1000,
                    DataReceived = 500,
                    Percentage = 50,
                    DownloadSpeed = 100,
                    TimeRemaining = 5,
                },
                snapshot
            );
        }

        if (!withProgress || status == DownloadStatus.Paused)
            await sut.OnStatusChangedAsync(key, status, CancellationToken);
        if (status == DownloadStatus.MovePaused)
            sut.NotifyFileTransferProgress(key);
        if (status == DownloadStatus.Paused)
            sut.OnProgressUpdated(
                key,
                new DownloadTaskProgress
                {
                    DataTotal = 1000,
                    DataReceived = 999,
                    Percentage = 99.9m,
                    DownloadSpeed = 999,
                },
                snapshot with
                {
                    SaveProgress = 99.9,
                }
            );
        await sut.StartAsync(CancellationToken.None);
        try
        {
            await patchReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }

        // Assert
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.Id.ShouldBe(target.Id);
        persisted.ToParentKey()!.Id.ShouldBe(ancestorIds[0]);
        persisted.DownloadStatus.ShouldBe(status);
        persisted.DataReceived.ShouldBe(withProgress ? 500 : 0);
        persisted.DataTotal.ShouldBe(1000);
        persisted.DownloadSpeed.ShouldBe(withProgress && status != DownloadStatus.Paused ? 100 : 0);
        persisted.FileDataTransferred.ShouldBe(status == DownloadStatus.MovePaused ? 250 : 0);
        if (withProgress)
        {
            persisted.DirectDownloadSnapshot.ShouldNotBeNull();
            persisted.DirectDownloadSnapshot!.SaveProgress.ShouldBe(50);
            persisted.DirectDownloadSnapshot.FileName.ShouldBe(target.FileName);
            persisted.DirectDownloadSnapshot.Urls.ShouldBe(snapshot.Urls);
            persisted
                .DirectDownloadSnapshot.Chunks.Select(x => (x.Id, x.Start, x.End, x.Position))
                .ShouldBe([("target-chunk", 0L, 999L, 500L)]);
        }

        var retained = (await dbContext.GetDownloadTaskFileAsync(sibling.ToKey(), CancellationToken))!;
        retained.Id.ShouldBe(sibling.Id);
        retained.ToParentKey()!.Id.ShouldBe(ancestorIds[0]);
        retained.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        retained.DataReceived.ShouldBe(0);
        retained.DirectDownloadSnapshot.ShouldBeNull();
        if (!withProgress || status == DownloadStatus.Paused)
        {
            var statusMessages = await dbContext
                .DownloadTaskTrackFileLogs.Where(x =>
                    x.DownloadTaskFileId == target.Id && x.Message.StartsWith("Download ")
                )
                .Select(x => x.Message)
                .ToArrayAsync(CancellationToken);
            statusMessages.ShouldBe([$"Download {target.FileName} transitioned to status: {status}"]);
        }

        var dispatched = received.ToArray();
        dispatched.ShouldNotBeEmpty();
        dispatched
            .Select(x => x.Sequence)
            .Order()
            .ShouldBe(Enumerable.Range(1, dispatched.Length).Select(x => (long)x));
        foreach (var (_, patches) in dispatched)
        {
            patches.Select(x => x.Id).Order().ShouldBe(expectedIds);
            var leaf = patches.Single(x => x.Id == target.Id);
            leaf.ParentId.ShouldBe(ancestorIds[0]);
            leaf.Status.ShouldBe(status);
            leaf.DataReceived.ShouldBe(withProgress ? 500 : 0);
            leaf.DataTotal.ShouldBe(1000);
            leaf.Percentage.ShouldBe(
                status == DownloadStatus.MovePaused ? 25
                : withProgress ? 50
                : 0
            );
            foreach (var ancestorId in ancestorIds)
            {
                var ancestor = patches.Single(x => x.Id == ancestorId);
                ancestor.DataTotal.ShouldBe(2000);
                ancestor.DataReceived.ShouldBe(withProgress ? 500 : 0);
            }
        }

        Mock.Mock<IDownloadHubService>()
            .Verify(
                x =>
                    x.SendDownloadPatchAsync(
                        target.PlexServerId,
                        It.IsAny<long>(),
                        It.Is<IReadOnlyCollection<DownloadPatchDTO>>(p =>
                            p.Select(n => n.Id).Order().SequenceEqual(expectedIds)
                        ),
                        null,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(dispatched.Length)
            );
        Mock.Mock<IDownloadHubService>()
            .Verify(
                x =>
                    x.SendDownloadPatchAsync(
                        It.Is<int>(id => id != target.PlexServerId),
                        It.IsAny<long>(),
                        It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                        It.IsAny<IReadOnlyCollection<Guid>?>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }
}
