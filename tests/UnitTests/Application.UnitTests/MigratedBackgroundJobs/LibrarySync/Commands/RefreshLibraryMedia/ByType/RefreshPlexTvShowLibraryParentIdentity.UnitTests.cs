namespace Reaparr.Application.UnitTests;

public class RefreshPlexTvShowLibraryParentIdentityUnitTests : BaseCommandUnitTest<RefreshPlexTvShowLibraryCommand>
{
    [Test]
    [Arguments(true, true)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task ShouldKeepChildrenWithTheirPlexParents_WhenParentGuidsAreSharedMissingOrLegacy(
        bool useRatingKeys,
        bool provideParentGuids
    )
    {
        // Arrange
        var seed = await SetupDatabase(
            625850,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 0;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.Include(x => x.TvShows).SingleAsync(CancellationToken);
        var shows = library.TvShows.OrderBy(x => x.PlexApiRatingKey).ToList();
        shows.Count.ShouldBe(2);
        shows[0].UpdateInitProperty(nameof(PlexTvShow.Guid), "plex://show/first");
        shows[1].UpdateInitProperty(nameof(PlexTvShow.Guid), useRatingKeys ? shows[0].Guid : "plex://show/second");
        var seasons = FakeData.GetPlexTvShowSeason(seed).Generate(2);
        var episodes = FakeData.GetPlexTvShowEpisode(seed).Generate(2);
        for (var index = 0; index < 2; index++)
        {
            seasons[index].PlexApiRatingKey = 1001 + index;
            seasons[index]
                .UpdateInitProperty(
                    nameof(PlexTvShowSeason.Guid),
                    useRatingKeys ? "plex://season/shared" : $"plex://season/{index}"
                );
            seasons[index].ParentKey = useRatingKeys ? shows[index].PlexApiRatingKey : -1;
            seasons[index].ParentGuid = provideParentGuids ? shows[index].Guid : null;
            seasons[index].Episodes.Clear();
            episodes[index].PlexApiRatingKey = 2001 + index;
            episodes[index].ParentKey = useRatingKeys ? seasons[index].PlexApiRatingKey : -1;
            episodes[index].ParentGuid = provideParentGuids ? seasons[index].Guid : null;
            episodes[index].MediaSize = 100 + index;
            episodes[index].Duration = 10 + index;
        }
        var metadata = new InsertMediaMetaDataCommandResponse(library);
        SyncPlexTvShowsCommand? captured = null;

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<GetAllMediaSeasonsCommand>(c => c.PlexLibrary == library), CancellationToken))
            .ReturnsAsync(Result.Ok(seasons))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<GetAllMediaEpisodesCommand>(c => c.PlexLibrary == library), CancellationToken))
            .ReturnsAsync(Result.Ok(episodes))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SyncPlexTvShowsCommand>(c => c.LibraryMetadata == metadata && !c.ForceMediaRefresh),
                    CancellationToken
                )
            )
            .Callback<ICommand<Result<BulkInsertTvShowsRapport>>, CancellationToken>(
                (command, _) => captured = (SyncPlexTvShowsCommand)command
            )
            .ReturnsAsync(Result.Ok(new BulkInsertTvShowsRapport()))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<ScheduleOptimizeDatabaseJobCommand>(c => c.ChangedItemCount == 0), CancellationToken)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand(PlexMediaType.TvShow))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(
                    library.Id,
                    It.Is<LibraryProgressItem>(item =>
                        (
                            item.MediaType == PlexMediaType.TvShow
                            || item.MediaType == PlexMediaType.Season
                            || item.MediaType == PlexMediaType.Episode
                        )
                        && item.Received == 2
                        && item.Total == 2
                        && item.TimeRemaining == TimeSpan.Zero
                    ),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(3));

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexTvShowLibraryCommand(metadata));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Id.ShouldBe(library.Id);
        captured.ShouldNotBeNull();
        captured
            .LibraryMetadata.PlexLibrary.TvShows.OrderBy(x => x.PlexApiRatingKey)
            .SelectMany(show =>
                show.Seasons.SelectMany(season =>
                    season.Episodes.Select(episode =>
                        (
                            ShowKey: show.PlexApiRatingKey,
                            SeasonKey: season.PlexApiRatingKey,
                            EpisodeKey: episode.PlexApiRatingKey,
                            episode.ParentKey,
                            episode.PlexLibraryId,
                            episode.PlexServerId
                        )
                    )
                )
            )
            .ShouldBe([
                (shows[0].PlexApiRatingKey, 1001, 2001, useRatingKeys ? 1001 : -1, library.Id, library.PlexServerId),
                (shows[1].PlexApiRatingKey, 1002, 2002, useRatingKeys ? 1002 : -1, library.Id, library.PlexServerId),
            ]);
        shows
            .Select(show => (show.ChildCount, show.GrandChildCount, show.MediaSize, show.Duration))
            .ShouldBe([(1, 1, 100L, episodes[0].Duration), (1, 1, 101L, episodes[1].Duration)]);
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }
}
