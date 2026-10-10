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
}
