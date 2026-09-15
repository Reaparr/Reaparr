namespace Reaparr.Application.UnitTests;

[NotInParallel]
public class SyncPlexTvShowsCommandUnitTests : BaseUnitTest<SyncPlexTvShowsCommandHandler>
{
    private readonly SyncPlexTvShowsCommandValidator _validator;

    public SyncPlexTvShowsCommandUnitTests()
    {
        _validator = new SyncPlexTvShowsCommandValidator(LogFactory.Create<SyncPlexTvShowsCommandValidator>());
    }

    [Test]
    public async Task ShouldMoveEpisodeBeforeDeletingItsOldSeason()
    {
        // Arrange
        await SetupDatabase(
            11878,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var show = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .Single();
        var oldSeason = show.Seasons.OrderBy(x => x.Id).First();
        var newSeason = show.Seasons.OrderBy(x => x.Id).Last();
        var episode = oldSeason.Episodes.Single();
        var episodeId = episode.Id;
        var episodeUpdatedAt = episode.UpdatedAt;
        var mediaDataCount = IDbContext.PlexTvShowEpisodeData.Count(x => x.PlexTvShowEpisodeId == episodeId);

        oldSeason.Episodes.Remove(episode);
        show.Seasons.Remove(oldSeason);
        episode.ParentKey = newSeason.PlexApiRatingKey;
        episode.ParentGuid = newSeason.Guid;
        newSeason.Episodes.Add(episode);
        SetIds(library, [show]);
        library.TvShows.Add(show);

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Errors.FirstOrDefault()?.Message);
        result.Value.CreatedTvShows.ShouldBe(0);
        result.Value.UpdatedTvShows.ShouldBe(0);
        result.Value.DeletedTvShows.ShouldBe(0);
        result.Value.UnchangedTvShows.ShouldBe(1);
        result.Value.CreatedSeasons.ShouldBe(0);
        result.Value.UpdatedSeasons.ShouldBe(0);
        result.Value.DeletedSeasons.ShouldBe(1);
        result.Value.UnchangedSeasons.ShouldBe(1);
        result.Value.CreatedEpisodes.ShouldBe(0);
        result.Value.UpdatedEpisodes.ShouldBe(1);
        result.Value.DeletedEpisodes.ShouldBe(0);
        result.Value.UnchangedEpisodes.ShouldBe(1);

        var persistedEpisode = IDbContext.PlexTvShowEpisodes.AsNoTracking().Single(x => x.Id == episodeId);
        persistedEpisode.TvShowSeasonId.ShouldBe(newSeason.Id);
        persistedEpisode.ParentKey.ShouldBe(newSeason.PlexApiRatingKey);
        persistedEpisode.UpdatedAt.ShouldBe(episodeUpdatedAt);
        IDbContext.PlexTvShowSeason.AsNoTracking().Any(x => x.Id == oldSeason.Id).ShouldBeFalse();
        IDbContext.PlexTvShowEpisodes.Count(x => x.PlexLibraryId == library.Id).ShouldBe(2);
        IDbContext.PlexTvShowEpisodeData.Count(x => x.PlexTvShowEpisodeId == episodeId).ShouldBe(mediaDataCount);
    }

    [Test]
    public async Task ShouldMoveSeasonAndEpisodesBeforeDeletingItsOldShow()
    {
        // Arrange
        await SetupDatabase(
            11879,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var shows = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToList();
        var oldShow = shows.First();
        var newShow = shows.Last();
        var season = oldShow.Seasons.Single();
        var episode = season.Episodes.Single();
        var seasonId = season.Id;
        var episodeId = episode.Id;
        var seasonUpdatedAt = season.UpdatedAt;
        var episodeUpdatedAt = episode.UpdatedAt;

        oldShow.Seasons.Remove(season);
        season.ParentKey = newShow.PlexApiRatingKey;
        season.ParentGuid = newShow.Guid;
        newShow.Seasons.Add(season);
        SetIds(library, [newShow]);
        library.TvShows.Add(newShow);

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Errors.FirstOrDefault()?.Message);
        result.Value.CreatedTvShows.ShouldBe(0);
        result.Value.UpdatedTvShows.ShouldBe(0);
        result.Value.DeletedTvShows.ShouldBe(1);
        result.Value.UnchangedTvShows.ShouldBe(1);
        result.Value.CreatedSeasons.ShouldBe(0);
        result.Value.UpdatedSeasons.ShouldBe(1);
        result.Value.DeletedSeasons.ShouldBe(0);
        result.Value.UnchangedSeasons.ShouldBe(1);
        result.Value.CreatedEpisodes.ShouldBe(0);
        result.Value.UpdatedEpisodes.ShouldBe(1);
        result.Value.DeletedEpisodes.ShouldBe(0);
        result.Value.UnchangedEpisodes.ShouldBe(1);

        IDbContext.PlexTvShows.AsNoTracking().Any(x => x.Id == oldShow.Id).ShouldBeFalse();
        var persistedSeason = IDbContext.PlexTvShowSeason.AsNoTracking().Single(x => x.Id == seasonId);
        persistedSeason.TvShowId.ShouldBe(newShow.Id);
        persistedSeason.ParentKey.ShouldBe(newShow.PlexApiRatingKey);
        persistedSeason.UpdatedAt.ShouldBe(seasonUpdatedAt);
        var persistedEpisode = IDbContext.PlexTvShowEpisodes.AsNoTracking().Single(x => x.Id == episodeId);
        persistedEpisode.TvShowId.ShouldBe(newShow.Id);
        persistedEpisode.TvShowSeasonId.ShouldBe(seasonId);
        persistedEpisode.UpdatedAt.ShouldBe(episodeUpdatedAt);
        IDbContext.PlexTvShows.Count(x => x.PlexLibraryId == library.Id).ShouldBe(1);
        IDbContext.PlexTvShowSeason.Count(x => x.PlexLibraryId == library.Id).ShouldBe(2);
        IDbContext.PlexTvShowEpisodes.Count(x => x.PlexLibraryId == library.Id).ShouldBe(2);
    }

    [Test]
    public async Task ShouldCreateAllTvShows_WhenNoneExists()
    {
        // Arrange
        var seed = await SetupDatabase(
            11865,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        library.ShouldNotBeNull();

        var newTvShows = FakeData
            .GetPlexTvShows(
                seed,
                config =>
                {
                    config.TvShowSeasonCount = 2;
                    config.TvShowEpisodeCount = 5;
                }
            )
            .Generate(10);

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var dbPlexTvShows = IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .Where(x => x.PlexLibraryId == library.Id)
            .ToList();

        var dbSeasons = dbPlexTvShows.SelectMany(x => x.Seasons).ToList();
        var dbEpisodes = dbSeasons.SelectMany(x => x.Episodes).ToList();
        var dbEpisodeMediaDataCount = IDbContext.PlexTvShowEpisodeData.Count(x => x.PlexLibraryId == library.Id);

        dbPlexTvShows.Count.ShouldBe(10);
        dbSeasons.Count.ShouldBe(20);
        dbEpisodes.Count.ShouldBe(100);
        dbEpisodeMediaDataCount.ShouldBe(100);

        var dbLibrary = IDbContext.PlexLibraries.First(x => x.Id == library.Id);
        dbLibrary.TvShowCount.ShouldBe(dbPlexTvShows.Count);
        dbLibrary.SeasonCount.ShouldBe(dbSeasons.Count);
        dbLibrary.EpisodeCount.ShouldBe(dbEpisodes.Count);
        dbLibrary.EpisodeMediaDataCount.ShouldBe(dbEpisodeMediaDataCount);
        VerifyKeys(newTvShows);
    }

    [Test]
    public async Task ShouldDeleteAllTvMedia_WhenSuccessfulResultIsEmpty()
    {
        // Arrange
        await SetupDatabase(
            11867,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 3;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 2;
            }
        );

        var library = IDbContext.PlexLibraries.First();

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.DeletedTvShows.ShouldBe(3);
        result.Value.DeletedSeasons.ShouldBe(6);
        result.Value.DeletedEpisodes.ShouldBe(12);
        IDbContext.PlexTvShows.Count(x => x.PlexLibraryId == library.Id).ShouldBe(0);
        IDbContext.PlexTvShowSeason.Count(x => x.PlexLibraryId == library.Id).ShouldBe(0);
        IDbContext.PlexTvShowEpisodes.Count(x => x.PlexLibraryId == library.Id).ShouldBe(0);
    }

    [Test]
    public async Task ShouldReconcileHierarchyLevelsIndependentlyAndPreserveMatchedIds()
    {
        // Arrange
        var seed = await SetupDatabase(
            11868,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 2;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        var show = IDbContext.PlexTvShows.AsNoTracking().Include(x => x.Seasons).ThenInclude(x => x.Episodes).Single();
        var originalShowId = show.Id;
        var updatedSeason = show.Seasons.OrderBy(x => x.Id).First();
        var unchangedSeason = show.Seasons.OrderBy(x => x.Id).Last();
        var originalUpdatedSeasonId = updatedSeason.Id;
        var originalUnchangedSeasonId = unchangedSeason.Id;
        var updatedEpisode = updatedSeason.Episodes.OrderBy(x => x.Id).First();
        var unchangedEpisode = updatedSeason.Episodes.OrderBy(x => x.Id).Last();
        var originalUpdatedEpisodeId = updatedEpisode.Id;
        var originalUnchangedEpisodeId = unchangedEpisode.Id;
        updatedSeason.UpdatedAt = updatedSeason.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        updatedEpisode.UpdatedAt = updatedEpisode.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        var createdEpisode = FakeData.GetPlexTvShowEpisode(seed, x => x.IncludeMultiPartEpisodes = true).Generate();
        unchangedSeason.Episodes.Add(createdEpisode);
        SetIds(library, [show]);
        library.TvShows.Add(show);

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CreatedTvShows.ShouldBe(0);
        result.Value.UpdatedTvShows.ShouldBe(0);
        result.Value.DeletedTvShows.ShouldBe(0);
        result.Value.UnchangedTvShows.ShouldBe(1);
        result.Value.CreatedSeasons.ShouldBe(0);
        result.Value.UpdatedSeasons.ShouldBe(1);
        result.Value.DeletedSeasons.ShouldBe(0);
        result.Value.UnchangedSeasons.ShouldBe(1);
        result.Value.CreatedEpisodes.ShouldBe(1);
        result.Value.UpdatedEpisodes.ShouldBe(1);
        result.Value.DeletedEpisodes.ShouldBe(0);
        result.Value.UnchangedEpisodes.ShouldBe(3);
        result.Value.ToString().ShouldContain("UnchangedTvShows: 1");
        result.Value.ToString().ShouldContain("UnchangedSeasons: 1");
        result.Value.ToString().ShouldContain("UnchangedEpisodes: 3");

        var persisted = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .Single();
        persisted.Id.ShouldBe(originalShowId);
        persisted
            .Seasons.Single(x => x.PlexApiRatingKey == updatedSeason.PlexApiRatingKey)
            .Id.ShouldBe(originalUpdatedSeasonId);
        persisted
            .Seasons.Single(x => x.PlexApiRatingKey == unchangedSeason.PlexApiRatingKey)
            .Id.ShouldBe(originalUnchangedSeasonId);
        var persistedEpisodes = persisted.Seasons.SelectMany(x => x.Episodes).ToList();
        persistedEpisodes
            .Single(x => x.PlexApiRatingKey == updatedEpisode.PlexApiRatingKey)
            .Id.ShouldBe(originalUpdatedEpisodeId);
        persistedEpisodes
            .Single(x => x.PlexApiRatingKey == unchangedEpisode.PlexApiRatingKey)
            .Id.ShouldBe(originalUnchangedEpisodeId);
        var persistedCreatedEpisode = persistedEpisodes.Single(x =>
            x.PlexApiRatingKey == createdEpisode.PlexApiRatingKey
        );
        persistedCreatedEpisode.Id.ShouldBeGreaterThan(0);
        persistedCreatedEpisode.TvShowId.ShouldBe(persisted.Id);
        persistedCreatedEpisode.TvShowSeasonId.ShouldBe(originalUnchangedSeasonId);
    }

    [Test]
    public async Task ShouldPreserveUnchangedEpisodeMediaAndReplaceOnlyChangedEpisodeMedia()
    {
        // Arrange
        await SetupDatabase(
            11869,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
                config.IncludeMultiPartEpisodes = true;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        var show = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .Single();
        var episodes = show.Seasons.Single().Episodes.OrderBy(x => x.Id).ToList();
        var unchanged = episodes[0];
        var changed = episodes[1];
        var unchangedMediaIds = unchanged.MediaDataList.Select(x => x.Id).Order().ToList();
        var oldChangedMediaIds = changed.MediaDataList.Select(x => x.Id).ToHashSet();
        unchanged.MediaDataList.Remove(unchanged.MediaDataList.First());
        changed.UpdatedAt = changed.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        SetIds(library, [show]);
        library.TvShows.Add(show);

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UpdatedEpisodes.ShouldBe(1);
        var persistedEpisodes = IDbContext.PlexTvShowEpisodes.AsNoTracking().Include(x => x.MediaDataList).ToList();
        persistedEpisodes
            .Single(x => x.PlexApiRatingKey == unchanged.PlexApiRatingKey)
            .MediaDataList.Select(x => x.Id)
            .Order()
            .ShouldBe(unchangedMediaIds);
        persistedEpisodes
            .Single(x => x.PlexApiRatingKey == changed.PlexApiRatingKey)
            .MediaDataList.ShouldAllBe(x => !oldChangedMediaIds.Contains(x.Id));
        var persistedMediaDataCount = persistedEpisodes.Sum(x => x.MediaDataList.Count);
        IDbContext
            .PlexLibraries.Single(x => x.Id == library.Id)
            .EpisodeMediaDataCount.ShouldBe(persistedMediaDataCount);
    }

    [Test]
    public async Task ShouldNotDeleteTvMediaFromOtherLibraries_WhenTargetResultIsEmpty()
    {
        // Arrange
        await SetupDatabase(
            11870,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 2;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var libraries = IDbContext.PlexLibraries.OrderBy(x => x.Id).ToList();
        var target = libraries[0];
        var other = libraries[1];
        var otherShowIds = IDbContext
            .PlexTvShows.Where(x => x.PlexLibraryId == other.Id)
            .Select(x => x.Id)
            .Order()
            .ToList();

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(target)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        IDbContext.PlexTvShows.Count(x => x.PlexLibraryId == target.Id).ShouldBe(0);
        IDbContext.PlexTvShows.Where(x => x.PlexLibraryId == other.Id).Select(x => x.Id).Order().ShouldBe(otherShowIds);
    }

    [Test]
    public async Task ShouldBeIdempotent_WhenSameChangedHierarchyIsRepeated()
    {
        // Arrange
        await SetupDatabase(
            11871,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var show = IDbContext.PlexTvShows.AsNoTracking().Include(x => x.Seasons).ThenInclude(x => x.Episodes).Single();
        show.Seasons.Single().Episodes.Single().UpdatedAt = DateTime.UtcNow.AddYears(1);
        SetIds(library, [show]);
        library.TvShows.Add(show);
        var command = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));

        // Act
        var firstResult = await Sut.ExecuteAsync(command, CancellationToken);
        var secondResult = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        firstResult.IsSuccess.ShouldBeTrue();
        firstResult.Value.UpdatedEpisodes.ShouldBe(1);
        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Value.CreatedTvShows.ShouldBe(0);
        secondResult.Value.UpdatedTvShows.ShouldBe(0);
        secondResult.Value.DeletedTvShows.ShouldBe(0);
        secondResult.Value.CreatedSeasons.ShouldBe(0);
        secondResult.Value.UpdatedSeasons.ShouldBe(0);
        secondResult.Value.DeletedSeasons.ShouldBe(0);
        secondResult.Value.CreatedEpisodes.ShouldBe(0);
        secondResult.Value.UpdatedEpisodes.ShouldBe(0);
        secondResult.Value.DeletedEpisodes.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReplaceAllTvShows_WhenForceMediaRefreshIsTrue()
    {
        // Arrange
        await SetupDatabase(
            11873,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var shows = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToList();
        var originalIds = shows.Select(x => x.Id).ToList();
        SetIds(library, shows);
        library.TvShows.AddRange(shows);
        var command = new SyncPlexTvShowsCommand(
            new InsertMediaMetaDataCommandResponse(library),
            ForceMediaRefresh: true
        );

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Errors.FirstOrDefault()?.Message);
        var replacedShows = IDbContext.PlexTvShows.AsNoTracking().OrderBy(x => x.Id).ToList();
        replacedShows.Count.ShouldBe(2);
        replacedShows.Select(x => x.Id).ShouldAllBe(id => !originalIds.Contains(id));
    }

    [Test]
    public async Task ShouldLeaveTvHierarchyUnchanged_WhenAlreadyCancelled()
    {
        // Arrange
        await SetupDatabase(
            11872,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var originalShowIds = IDbContext.PlexTvShows.Select(x => x.Id).Order().ToList();
        var originalSeasonIds = IDbContext.PlexTvShowSeason.Select(x => x.Id).Order().ToList();
        var originalEpisodeIds = IDbContext.PlexTvShowEpisodes.Select(x => x.Id).Order().ToList();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library)),
            cancellationTokenSource.Token
        );

        // Assert
        result.IsCancelled.ShouldBeTrue();
        IDbContext.PlexTvShows.Select(x => x.Id).Order().ShouldBe(originalShowIds);
        IDbContext.PlexTvShowSeason.Select(x => x.Id).Order().ShouldBe(originalSeasonIds);
        IDbContext.PlexTvShowEpisodes.Select(x => x.Id).Order().ShouldBe(originalEpisodeIds);
    }

    [Test]
    public async Task ShouldSynchronizeRelationshipsForCompleteIncomingShowTree_WhenShowsAreUnchanged()
    {
        // Arrange
        var seed = await SetupDatabase(
            11873,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var shows = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .OrderBy(x => x.Id)
            .ToList();
        var actor = FakeData.GetPlexActors(seed).Generate();
        await IDbContext.BulkInsertAsync([actor], cancellationToken: CancellationToken);
        actor.Id = IDbContext.PlexActors.AsNoTracking().Single(x => x.Key == actor.Key).Id;
        foreach (var show in shows)
            show.Actors.Add(actor);
        library.TvShows.AddRange(shows);
        var metadata = new InsertMediaMetaDataCommandResponse(library)
        {
            PlexActors = new Dictionary<string, PlexActor> { [actor.Key] = actor },
        };

        // Act
        var result = await Sut.ExecuteAsync(new SyncPlexTvShowsCommand(metadata), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UpdatedTvShows.ShouldBe(0);
        var relations = IDbContext.PlexTvShowActors.Where(x => x.PlexLibraryId == library.Id).ToList();
        relations.Count.ShouldBe(2);
        relations.Select(x => x.PlexTvShowId).Order().ShouldBe(shows.Select(x => x.Id).Order());
        relations.ShouldAllBe(x => x.PlexActorId == actor.Id);
    }

    [Test]
    public async Task ShouldUpdateAllTvShows_WhenAllExists()
    {
        // Arrange
        await SetupDatabase(
            48674,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 10;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 5;
            }
        );

        var library = IDbContext
            .PlexLibraries.Include(x => x.TvShows)
                .ThenInclude(x => x.Seasons)
                    .ThenInclude(x => x.Episodes)
            .First();
        library.ShouldNotBeNull();

        foreach (var x in library.TvShows)
        {
            x.FullTitle = "TEST";
            x.UpdatedAt = x.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        }

        var newTvShows = library.TvShows.ToList();
        SetIds(library, newTvShows);
        library.TvShows.Clear();
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var plexTvShows = IDbContext.PlexTvShows.ToList();
        plexTvShows.Count.ShouldBe(10);
        foreach (var plexTvShow in plexTvShows)
            plexTvShow.FullTitle.ShouldBe("TEST");

        VerifyKeys(library.TvShows);
    }

    [Test]
    public async Task ShouldDeleteTwentyTvShows_WhenSomeExist()
    {
        // Arrange
        await SetupDatabase(
            4503253,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 50;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        library.ShouldNotBeNull();
        var tvShows = IDbContext.PlexTvShows.Include(x => x.Seasons).ThenInclude(x => x.Episodes).ToList();
        tvShows.ShouldNotBeNull();

        var newTvShows = tvShows.GetRange(0, 30);

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        VerifyKeys(newTvShows);
    }

    [Test]
    public async Task ShouldInsertSeasonsWithExistingTvShows_WhenSomeSeasonsAlreadyExist()
    {
        // Arrange
        var seed = await SetupDatabase(
            34284,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 10;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 5;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        library.ShouldNotBeNull();
        var newTvShows = IDbContext.PlexTvShows.Include(x => x.Seasons).ThenInclude(x => x.Episodes).ToList();

        foreach (var tvShow in newTvShows)
            tvShow.Seasons.AddRange(FakeData.GetPlexTvShowSeason(seed).Generate(3));

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // var dbTvShows = IDbContext.PlexTvShows.Include(x => x.Seasons).ThenInclude(x => x.Episodes).ToList();

        VerifyKeys(newTvShows);
    }

    [Test]
    public async Task ShouldNotCreateDuplicateKeys_WhenTheSameMediaIsCreated()
    {
        // Arrange
        await SetupDatabase(
            16344,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 10;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 5;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        library.ShouldNotBeNull();
        var newTvShows = IDbContext.PlexTvShows.Include(x => x.Seasons).ThenInclude(x => x.Episodes).ToList();

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Sut.ExecuteAsync(request, CancellationToken);
        var result2 = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();

        VerifyKeys(newTvShows);
    }

    [Test]
    public async Task ShouldCreateUpdateAndDeleteMovies_WhenSomeExist()
    {
        // Arrange
        var seed = await SetupDatabase(
            85746,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 50;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        var tvShowsDb = IDbContext.PlexTvShows.ToList();
        library.ShouldNotBeNull();

        var newTvShows = new List<PlexTvShow>();
        newTvShows.AddRange(tvShowsDb.GetRange(0, 10));
        newTvShows.AddRange(FakeData.GetPlexTvShows(seed).Generate(30));

        for (var i = 0; i < newTvShows.Count; i++)
        {
            // Create updated TvShows based on the Key
            if (i is >= 10 and < 30)
            {
                newTvShows[i].PlexApiRatingKey = tvShowsDb[i].PlexApiRatingKey;
                newTvShows[i].UpdatedAt = DateTime.UtcNow;
            }

            newTvShows[i].Id = 0;
            newTvShows[i].Title = $"TEST - {newTvShows[i].Title}";
        }

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var plexTvShows = IDbContext.PlexTvShows.ToList();
        plexTvShows.Count.ShouldBe(40);
        foreach (var plexTvShow in newTvShows)
        {
            var findResult = plexTvShows.Find(x => x.PlexApiRatingKey == plexTvShow.PlexApiRatingKey);
            findResult.ShouldNotBeNull();
            if (
                plexTvShow.UpdatedAt
                != tvShowsDb.Find(x => x.PlexApiRatingKey == plexTvShow.PlexApiRatingKey)?.UpdatedAt
            )
                findResult.Title.Contains("TEST").ShouldBeTrue();
        }

        result.Value.CreatedTvShows.ShouldBe(10);
        result.Value.UpdatedTvShows.ShouldBe(20);
        result.Value.DeletedTvShows.ShouldBe(20);
        VerifyKeys(newTvShows);
    }

    private void VerifyKeys(ICollection<PlexTvShow> newTvShows)
    {
        var plexLibraryId = newTvShows.First().PlexLibraryId;
        var dbPlexTvShows = IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .Where(x => x.PlexLibraryId == plexLibraryId)
            .ToList();
        var dbSeasons = dbPlexTvShows.SelectMany(x => x.Seasons).ToList();
        var dbEpisodes = dbSeasons.SelectMany(x => x.Episodes).ToList();

        dbPlexTvShows.All(x => newTvShows.Any(y => y.PlexApiRatingKey == x.PlexApiRatingKey)).ShouldBeTrue();
        dbSeasons
            .All(x => newTvShows.SelectMany(y => y.Seasons).Any(y => y.PlexApiRatingKey == x.PlexApiRatingKey))
            .ShouldBeTrue();
        dbEpisodes
            .All(x =>
                newTvShows
                    .SelectMany(y => y.Seasons.SelectMany(z => z.Episodes))
                    .Any(y => y.PlexApiRatingKey == x.PlexApiRatingKey)
            )
            .ShouldBeTrue();
    }

    private void SetIds(PlexLibrary library, ICollection<PlexTvShow> newTvShows)
    {
        foreach (var plexTvShow in newTvShows)
        {
            plexTvShow.PlexLibraryId = library.Id;
            plexTvShow.PlexServerId = library.PlexServerId;
            foreach (var season in plexTvShow.Seasons)
            {
                season.PlexLibraryId = library.Id;
                season.PlexServerId = library.PlexServerId;

                foreach (var episode in season.Episodes)
                {
                    episode.PlexLibraryId = library.Id;
                    episode.PlexServerId = library.PlexServerId;
                }
            }
        }
    }
}
