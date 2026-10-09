namespace Reaparr.Application.UnitTests;

public class StopDownloadTaskCommandMusicUnitTests : BaseCommandUnitTest<StopDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadTaskType.MusicArtist, false)]
    [Arguments(DownloadTaskType.MusicAlbum, false)]
    [Arguments(DownloadTaskType.MusicTrack, false)]
    [Arguments(DownloadTaskType.MusicTrackData, false)]
    [Arguments(DownloadTaskType.MusicTrackData, true)]
    public async Task ShouldStopOnlyActiveSelectedMusicOrPreserveStateOnFailure_WhenStopping(
        DownloadTaskType selection,
        bool failStop
    )
    {
        // Arrange
        await SetupDatabase(
            88220,
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
        var musicFiles = await dbContext.DownloadTaskMusicTrackFiles
            .Include(x => x.Parent)
                .ThenInclude(x => x!.Parent)
                    .ThenInclude(x => x!.Parent)
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        DownloadTaskFileBase target = musicFiles[0];
        DownloadTaskFileBase control = musicFiles[1];
        DownloadTaskBase node = selection switch
        {
            DownloadTaskType.MusicArtist => ((DownloadTaskMusicTrackFile)target).Parent!.Parent!.Parent!,
            DownloadTaskType.MusicAlbum => ((DownloadTaskMusicTrackFile)target).Parent!.Parent!,
            DownloadTaskType.MusicTrack => ((DownloadTaskMusicTrackFile)target).Parent!,
            _ => target,
        };
        if (selection == DownloadTaskType.MusicArtist)
            await dbContext
                .DownloadTaskMusicAlbums.Where(x => x.Id == ((DownloadTaskMusicTrackFile)control).Parent!.ParentId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.ParentId, node.Id), CancellationToken);
        if (selection == DownloadTaskType.MusicAlbum)
            await dbContext
                .DownloadTaskMusicTracks.Where(x => x.Id == ((DownloadTaskMusicTrackFile)control).ParentId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.ParentId, node.Id), CancellationToken);
        target.DataReceived = 128;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.SetDownloadStatus(target.ToKey(), DownloadStatus.Downloading);
        var selected = await dbContext.GetDownloadableChildTaskKeys(node.ToKey(), CancellationToken);
        selected
            .Select(x => x.Id)
            .Order()
            .ShouldBe(
                (
                    selection is DownloadTaskType.MusicArtist or DownloadTaskType.MusicAlbum
                        ? new[] { target.Id, control.Id }
                        : new[] { target.Id }
                ).Order()
            );
        var error = new Error("stop rejected");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        foreach (var key in selected)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsDownloading(key, CancellationToken))
                .ReturnsAsync(key.Id == target.Id)
                .Verifiable(Times.Once());
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(key, CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
        }
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(target.ToKey(), CancellationToken))
            .ReturnsAsync(failStop ? Result.Fail(error) : Result.Ok())
            .Verifiable(Times.Once());
        if (!failStop)
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(
                        It.Is<DeleteDownloadTaskFilesCommand>(c => c.Keys.SequenceEqual(new[] { target.ToKey() })),
                        CancellationToken
                    )
                )
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(node.Id));

        // Assert
        result.IsSuccess.ShouldBe(!failStop);
        result.Errors.Count.ShouldBe(failStop ? 1 : 0);
        if (failStop)
            result.Errors.Single().ShouldBeSameAs(error);
        var after = (await dbContext.GetDownloadTaskFileAsync(target.ToKey(), CancellationToken))!;
        after.DownloadStatus.ShouldBe(failStop ? DownloadStatus.Downloading : DownloadStatus.Stopped);
        after.DataReceived.ShouldBe(failStop ? 128 : 0);
        (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.Queued
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StopDownloadTaskJob(control.ToKey(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTaskFilesCommand>(c => c.Keys.Any(k => k.Id == control.Id)),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }
}
