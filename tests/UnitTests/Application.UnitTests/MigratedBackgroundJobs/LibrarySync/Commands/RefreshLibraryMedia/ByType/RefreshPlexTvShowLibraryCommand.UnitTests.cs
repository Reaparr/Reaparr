namespace Reaparr.Application.UnitTests;

public class RefreshPlexTvShowLibraryCommandUnitTests : BaseUnitTest<RefreshPlexTvShowLibraryCommandHandler>
{
    private void SetupProgressStoreMocks(List<LibraryProgressItem>? capturedItems = null)
    {
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>())
            )
            .Callback<int, LibraryProgressItem, CancellationToken>((_, item, _) => capturedItems?.Add(item))
            .Returns(Task.CompletedTask);

        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private (List<PlexTvShowSeason> Seasons, List<PlexTvShowEpisode> Episodes) BuildSeasonsAndEpisodes(
        Seed seed,
        int seasonCount = 6,
        int episodesPerSeason = 30
    )
    {
        var seasons = FakeData.GetPlexTvShowSeason(seed).Generate(seasonCount);
        var episodes = new List<PlexTvShowEpisode>();
        foreach (var season in seasons)
        {
            var eps = FakeData.GetPlexTvShowEpisode(seed).Generate(episodesPerSeason);
            foreach (var ep in eps)
            {
                ep.ParentGuid = season.Guid;
                ep.MediaSize = 150_000_000;
                ep.Duration = 1800;
            }

            episodes.AddRange(eps);
        }

        return (seasons, episodes);
    }

    private void SetupCommandExecutorForMedia(List<PlexTvShowSeason> seasons, List<PlexTvShowEpisode> episodes)
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetAllMediaSeasonsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(seasons));

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetAllMediaEpisodesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(episodes));
    }

    private void SetupMediaOverviewRebuild()
    {
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok());
        Mock.SetupCommand(() => new ScheduleOptimizeDatabaseJobCommand { ChangedItemCount = 0 })
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
    }

    private void SetupSyncCommandSuccess(BulkInsertTvShowsRapport? rapport = null)
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SyncPlexTvShowsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(rapport ?? new BulkInsertTvShowsRapport()));
    }

    private void SetupSyncCommandCapture(
        Action<SyncPlexTvShowsCommand> capture,
        BulkInsertTvShowsRapport? rapport = null
    )
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SyncPlexTvShowsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result<BulkInsertTvShowsRapport>>, CancellationToken>(
                (cmd, _) => capture((SyncPlexTvShowsCommand)cmd)
            )
            .ReturnsAsync(Result.Ok(rapport ?? new BulkInsertTvShowsRapport()));
    }

    [Test]
    public async Task ShouldSuccessfullyRefreshLibraryAndKeepSyncedAtUnchanged_WhenTvShowsExist()
    {
        // Arrange
        var seed = await SetupDatabase(
            9423,
            config =>
            {
                config.TvShowCount = 3;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 5;
            }
        );
        var dbContext = IDbContext;
        var testLibrary = dbContext.PlexLibraries.Include(x => x.TvShows).First();
        var originalSyncedAt = testLibrary.SyncedAt;

        SetupProgressStoreMocks();

        var (seasons, episodes) = BuildSeasonsAndEpisodes(seed);
        SetupCommandExecutorForMedia(seasons, episodes);
        SetupSyncCommandSuccess();
        SetupMediaOverviewRebuild();

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var updatedLibrary = await dbContext.PlexLibraries.GetAsync(testLibrary.Id, CancellationToken);
        updatedLibrary.ShouldNotBeNull();
        updatedLibrary.SyncedAt.ShouldBe(originalSyncedAt);
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3)
            );
    }

    [Test]
    public async Task ShouldSendProgressWithSeasonAndEpisodeItems_WhenTvShowsExist()
    {
        // Arrange
        var seed = await SetupDatabase(
            9423,
            config =>
            {
                config.TvShowCount = 3;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 5;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();
        var capturedItems = new List<LibraryProgressItem>();

        SetupProgressStoreMocks(capturedItems);

        var (seasons, episodes) = BuildSeasonsAndEpisodes(seed);
        SetupCommandExecutorForMedia(seasons, episodes);
        SetupSyncCommandSuccess();
        SetupMediaOverviewRebuild();

        // Act
        await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        capturedItems.ShouldNotBeEmpty();
        capturedItems.ShouldContain(i => i.MediaType == PlexMediaType.TvShow);
        capturedItems.ShouldContain(i => i.MediaType == PlexMediaType.Season);
        capturedItems.ShouldContain(i => i.MediaType == PlexMediaType.Episode);
    }

    [Test]
    public async Task ShouldReturnFailure_WhenGetAllMediaSeasonsCommandFails()
    {
        // Arrange
        await SetupDatabase(
            9426,
            config =>
            {
                config.TvShowCount = 3;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();

        SetupProgressStoreMocks();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetAllMediaSeasonsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Failed to get seasons"));

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.First().Message.ShouldContain("Failed to get seasons");
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldReturnFailure_WhenGetAllMediaEpisodesCommandFails()
    {
        // Arrange
        var seed = await SetupDatabase(
            9427,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 3;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();

        SetupProgressStoreMocks();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetAllMediaSeasonsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(FakeData.GetPlexTvShowSeason(seed).Generate(6)));
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetAllMediaEpisodesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Failed to get episodes"));

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.First().Message.ShouldContain("Failed to get episodes");
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldReturnFailure_WhenSyncPlexTvShowsCommandFails()
    {
        // Arrange
        var seed = await SetupDatabase(
            9428,
            config =>
            {
                config.TvShowCount = 3;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();

        SetupProgressStoreMocks();

        var (seasons, episodes) = BuildSeasonsAndEpisodes(seed);
        SetupCommandExecutorForMedia(seasons, episodes);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SyncPlexTvShowsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Failed to sync TV shows"));

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.First().Message.ShouldContain("Failed to sync TV shows");
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    // ── Filter: null ParentGuid ────────────────────────────────────────────

    [Test]
    public async Task ShouldFilterOutSeasonsWithNullParentGuid_WhenBuildingTree()
    {
        // Arrange
        var seed = await SetupDatabase(
            11001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();

        SetupProgressStoreMocks();

        var validSeason = FakeData.GetPlexTvShowSeason(seed).Generate(1).First();
        validSeason.ParentGuid = testLibrary.TvShows.First().Guid;

        var invalidSeason = FakeData.GetPlexTvShowSeason(seed).Generate(1).First();
        invalidSeason.ParentGuid = null; // should be filtered out

        SetupCommandExecutorForMedia([validSeason, invalidSeason], validSeason.Episodes.ToList());

        SyncPlexTvShowsCommand? capturedCommand = null;
        SetupSyncCommandCapture(cmd => capturedCommand = cmd);
        SetupMediaOverviewRebuild();

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedCommand.ShouldNotBeNull();
        var tvShow = capturedCommand.LibraryMetadata.PlexLibrary.TvShows.First();

        // Only the valid season (with ParentGuid set) should be assigned to the show
        tvShow.Seasons.ShouldHaveSingleItem();
        tvShow.Seasons.First().Guid.ShouldBe(validSeason.Guid);
    }

    [Test]
    public async Task ShouldRepairMissingSeasonNumber_WhenEpisodeParentIndexesAgree()
    {
        // Arrange
        var seed = await SetupDatabase(
            11004,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();
        SetupProgressStoreMocks();

        var season = FakeData.GetPlexTvShowSeason(seed).Generate(1).First();
        season.ParentGuid = testLibrary.TvShows.First().Guid;
        season.SeasonNumber = -1;
        var episodes = FakeData.GetPlexTvShowEpisode(seed).Generate(2);
        foreach (var episode in episodes)
        {
            episode.ParentGuid = season.Guid;
            episode.ExpectedSeasonNumber = 4;
        }

        SetupCommandExecutorForMedia([season], episodes);
        SyncPlexTvShowsCommand? capturedCommand = null;
        SetupSyncCommandCapture(cmd => capturedCommand = cmd);
        SetupMediaOverviewRebuild();

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedCommand.ShouldNotBeNull();
        capturedCommand.LibraryMetadata.PlexLibrary.TvShows.Single().Seasons.Single().SeasonNumber.ShouldBe(4);
    }

    [Test]
    public async Task ShouldKeepMissingSeasonNumber_WhenEpisodeParentIndexesConflict()
    {
        // Arrange
        var seed = await SetupDatabase(
            11005,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();
        SetupProgressStoreMocks();

        var season = FakeData.GetPlexTvShowSeason(seed).Generate(1).First();
        season.ParentGuid = testLibrary.TvShows.First().Guid;
        season.SeasonNumber = -1;
        var episodes = FakeData.GetPlexTvShowEpisode(seed).Generate(2);
        episodes[0].ParentGuid = season.Guid;
        episodes[0].ExpectedSeasonNumber = 4;
        episodes[1].ParentGuid = season.Guid;
        episodes[1].ExpectedSeasonNumber = 5;

        SetupCommandExecutorForMedia([season], episodes);
        SyncPlexTvShowsCommand? capturedCommand = null;
        SetupSyncCommandCapture(cmd => capturedCommand = cmd);
        SetupMediaOverviewRebuild();

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedCommand.ShouldNotBeNull();
        capturedCommand.LibraryMetadata.PlexLibrary.TvShows.Single().Seasons.Single().SeasonNumber.ShouldBe(-1);
    }

    [Test]
    public async Task ShouldFilterOutEpisodesWithNullParentGuid_WhenBuildingTree()
    {
        // Arrange
        var seed = await SetupDatabase(
            11002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();

        SetupProgressStoreMocks();

        var season = FakeData.GetPlexTvShowSeason(seed).Generate(1).First();
        season.ParentGuid = testLibrary.TvShows.First().Guid;

        var validEpisode = FakeData.GetPlexTvShowEpisode(seed).Generate(1).First();
        validEpisode.ParentGuid = season.Guid;

        var invalidEpisode = FakeData.GetPlexTvShowEpisode(seed).Generate(1).First();
        invalidEpisode.ParentGuid = null; // should be filtered out

        SetupCommandExecutorForMedia([season], [validEpisode, invalidEpisode]);

        SyncPlexTvShowsCommand? capturedCommand = null;
        SetupSyncCommandCapture(cmd => capturedCommand = cmd);
        SetupMediaOverviewRebuild();

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedCommand.ShouldNotBeNull();
        var tvShowSeason = capturedCommand.LibraryMetadata.PlexLibrary.TvShows.First().Seasons.First();

        // Only the valid episode (with ParentGuid set) should be assigned to the season
        tvShowSeason.Episodes.ShouldHaveSingleItem();
        tvShowSeason.Episodes.First().Guid.ShouldBe(validEpisode.Guid);
    }

    // ── BuildTvShowTree: orphaned seasons ─────────────────────────────────

    [Test]
    public async Task ShouldIgnoreOrphanedSeasons_WhenParentTvShowDoesNotExist()
    {
        // Arrange
        var seed = await SetupDatabase(
            11003,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();

        SetupProgressStoreMocks();

        // Season whose ParentGuid does not match any TV show in the library
        var orphanedSeason = FakeData.GetPlexTvShowSeason(seed).Generate(1).First();
        orphanedSeason.ParentGuid = "plex://show/orphaned-guid-that-matches-nothing";

        SetupCommandExecutorForMedia([orphanedSeason], []);

        SyncPlexTvShowsCommand? capturedCommand = null;
        SetupSyncCommandCapture(cmd => capturedCommand = cmd);
        SetupMediaOverviewRebuild();

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedCommand.ShouldNotBeNull();

        // The TV show should have no seasons assigned (orphan was ignored)
        capturedCommand.LibraryMetadata.PlexLibrary.TvShows.First().Seasons.ShouldBeEmpty();
    }

    // ── BuildTvShowTree: aggregation ──────────────────────────────────────

    [Test]
    public async Task ShouldAggregateMediaSizeAndDurationFromEpisodes_WhenBuildingTree()
    {
        // Arrange
        var seed = await SetupDatabase(
            11004,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.TvShows).First();

        SetupProgressStoreMocks();

        var season = FakeData.GetPlexTvShowSeason(seed).Generate(1).First();
        season.ParentGuid = testLibrary.TvShows.First().Guid;
        season.Episodes.Clear();

        const long episodeSize = 100_000_000L;
        const int episodeDuration = 3600;

        var ep1 = FakeData.GetPlexTvShowEpisode(seed).Generate(1).First();
        ep1.ParentGuid = season.Guid;
        ep1.MediaSize = episodeSize;
        ep1.Duration = episodeDuration;

        var ep2 = FakeData.GetPlexTvShowEpisode(seed).Generate(1).First();
        ep2.ParentGuid = season.Guid;
        ep2.MediaSize = episodeSize;
        ep2.Duration = episodeDuration;

        SetupCommandExecutorForMedia([season], [ep1, ep2]);

        SyncPlexTvShowsCommand? capturedCommand = null;
        SetupSyncCommandCapture(cmd => capturedCommand = cmd);
        SetupMediaOverviewRebuild();

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexTvShowLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedCommand.ShouldNotBeNull();

        var tvShow = capturedCommand.LibraryMetadata.PlexLibrary.TvShows.First();
        var builtSeason = tvShow.Seasons.First();

        builtSeason.Episodes.Count.ShouldBe(2);
        builtSeason.MediaSize.ShouldBe(episodeSize * 2);
        builtSeason.Duration.ShouldBe(episodeDuration * 2);
        builtSeason.ChildCount.ShouldBe(2);

        tvShow.MediaSize.ShouldBe(episodeSize * 2);
        tvShow.GrandChildCount.ShouldBe(2);
    }
}
