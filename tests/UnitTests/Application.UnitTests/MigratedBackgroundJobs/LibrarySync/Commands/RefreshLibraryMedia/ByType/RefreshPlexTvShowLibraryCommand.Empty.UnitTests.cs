namespace Reaparr.Application.UnitTests;

[NotInParallel]
public class RefreshPlexTvShowLibraryCommandEmptyUnitTests : BaseCommandUnitTest<RefreshPlexTvShowLibraryCommand>
{
    [Test]
    public async Task ShouldRemoveOnlyTargetHierarchyAndResetMetrics_WhenConfirmedTvCatalogIsEmpty()
    {
        // Arrange
        await SetupDatabase(
            78131,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 2;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var target = libraries[0];
        var control = libraries[1];
        var showsBefore = await dbContext
            .PlexTvShows.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexLibraryId })
            .ToListAsync(CancellationToken);
        var seasonsBefore = await dbContext
            .PlexTvShowSeason.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexLibraryId,
                x.TvShowId,
            })
            .ToListAsync(CancellationToken);
        var episodesBefore = await dbContext
            .PlexTvShowEpisodes.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexLibraryId,
                x.TvShowSeasonId,
            })
            .ToListAsync(CancellationToken);
        showsBefore.Count(x => x.PlexLibraryId == target.Id).ShouldBe(1);
        seasonsBefore.Count(x => x.PlexLibraryId == target.Id).ShouldBe(1);
        episodesBefore.Count(x => x.PlexLibraryId == target.Id).ShouldBe(1);
        showsBefore.Count(x => x.PlexLibraryId == control.Id).ShouldBe(1);
        var retainedEpisodeIds = episodesBefore.Where(x => x.PlexLibraryId == control.Id).Select(x => x.Id).ToArray();
        var retainedData = await dbContext
            .PlexTvShowEpisodeData.Where(x => retainedEpisodeIds.Contains(x.PlexTvShowEpisodeId))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexTvShowEpisodeId })
            .ToListAsync(CancellationToken);
        var controlMetrics = (
            control.TvShowCount,
            control.SeasonCount,
            control.EpisodeCount,
            control.EpisodeMediaDataCount,
            control.MediaSize,
            control.MediaCount
        );
        target.TvShows.Clear();
        var syncHandler = Mock.Create<SyncPlexTvShowsCommandHandler>();
        var syncValidator = new SyncPlexTvShowsCommandValidator(LogFactory.Create<SyncPlexTvShowsCommandValidator>());
        var progress = new List<LibraryProgressItem>();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<GetAllMediaSeasonsCommand>(c => c.PlexLibrary.Id == target.Id), CancellationToken))
            .ReturnsAsync(Result.Ok(new List<PlexTvShowSeason>()))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GetAllMediaEpisodesCommand>(c => c.PlexLibrary.Id == target.Id), CancellationToken)
            )
            .ReturnsAsync(Result.Ok(new List<PlexTvShowEpisode>()))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SyncPlexTvShowsCommand>(c =>
                        c.LibraryMetadata.PlexLibraryId == target.Id
                        && c.LibraryMetadata.PlexLibrary.TvShows.Count == 0
                        && !c.ForceMediaRefresh
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (ICommand<Result<BulkInsertTvShowsRapport>> command, CancellationToken token) =>
                {
                    var sync = (SyncPlexTvShowsCommand)command;
                    return new ValidationPipeline<SyncPlexTvShowsCommand, Result<BulkInsertTvShowsRapport>>([
                        syncValidator,
                    ]).ExecuteAsync(sync, () => syncHandler.ExecuteAsync(sync, token), token);
                }
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<ScheduleOptimizeDatabaseJobCommand>(c => c.ChangedItemCount == 3), CancellationToken)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand(PlexMediaType.TvShow))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.UpdateItemAsync(target.Id, It.IsAny<LibraryProgressItem>()))
            .Callback<int, LibraryProgressItem>((_, item) => progress.Add(item))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(3));

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(target))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Id.ShouldBe(target.Id);
        (
            result.Value.TvShowCount,
            result.Value.SeasonCount,
            result.Value.EpisodeCount,
            result.Value.EpisodeMediaDataCount,
            result.Value.MediaSize,
            result.Value.MediaCount
        ).ShouldBe((0, 0, 0, 0, 0L, 0));
        (
            await dbContext
                .PlexTvShows.OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.PlexLibraryId })
                .ToListAsync(CancellationToken)
        ).ShouldBe(showsBefore.Where(x => x.PlexLibraryId == control.Id));
        (
            await dbContext
                .PlexTvShowSeason.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexLibraryId,
                    x.TvShowId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(seasonsBefore.Where(x => x.PlexLibraryId == control.Id));
        (
            await dbContext
                .PlexTvShowEpisodes.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexLibraryId,
                    x.TvShowSeasonId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(episodesBefore.Where(x => x.PlexLibraryId == control.Id));
        (
            await dbContext
                .PlexTvShowEpisodeData.OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.PlexTvShowEpisodeId })
                .ToListAsync(CancellationToken)
        ).ShouldBe(retainedData);
        var retained = await dbContext.PlexLibraries.SingleAsync(x => x.Id == control.Id, CancellationToken);
        (
            retained.TvShowCount,
            retained.SeasonCount,
            retained.EpisodeCount,
            retained.EpisodeMediaDataCount,
            retained.MediaSize,
            retained.MediaCount
        ).ShouldBe(controlMetrics);
        progress
            .Select(x => (x.MediaType, x.Received, x.Total))
            .ShouldBe([(PlexMediaType.TvShow, 0, 0), (PlexMediaType.Season, 0, 0), (PlexMediaType.Episode, 0, 0)]);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>()), Times.Never());
    }
}
