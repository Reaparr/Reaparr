namespace Reaparr.Application.UnitTests;

[NotInParallel]
public class SyncPlexTvShowsCommandUnitTests : BaseCommandUnitTest<SyncPlexTvShowsCommand>
{
    private readonly SyncPlexTvShowsCommandValidator _validator;

    public SyncPlexTvShowsCommandUnitTests()
    {
        _validator = Mock.Create<SyncPlexTvShowsCommandValidator>();
    }

    [Test]
    public async Task ShouldRejectInvalidServerThroughRealValidator_BeforeExecutingHandler()
    {
        // Arrange
        var library = new PlexLibrary
        {
            Id = 1,
            PlexServerId = 0,
            Key = "1",
            Type = PlexMediaType.TvShow,
            Title = "Validation library",
            Uuid = "validation-library",
            Language = "en",
            CreatedAt = null,
            UpdatedAt = null,
            ScannedAt = null,
            ContentChangedAt = 0,
        };
        var command = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));

        // Act
        var result = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        // The conversion retains the bad-request envelope and the concrete validation failure.
        result.Errors.Count.ShouldBe(2);
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
        var episodeUpdatedAt = episode.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        episode.UpdatedAt = episodeUpdatedAt;
        var mediaDataCount = IDbContext.PlexTvShowEpisodeData.Count(x => x.PlexTvShowEpisodeId == episodeId);

        oldSeason.Episodes.Remove(episode);
        show.Seasons.Remove(oldSeason);
        episode.ParentKey = newSeason.PlexApiRatingKey;
        episode.ParentGuid = newSeason.Guid;
        newSeason.Episodes.Add(episode);
        SetIds(library, [show]);
        library.TvShows.Add(show);

        // Act
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>()
            .ExecuteAsync(
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
        var seasonUpdatedAt = season.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        season.UpdatedAt = seasonUpdatedAt;
        var episodeUpdatedAt = episode.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        episode.UpdatedAt = episodeUpdatedAt;

        oldShow.Seasons.Remove(season);
        season.ParentKey = newShow.PlexApiRatingKey;
        season.ParentGuid = newShow.Guid;
        newShow.Seasons.Add(season);
        SetIds(library, [newShow]);
        library.TvShows.Add(newShow);

        // Act
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>()
            .ExecuteAsync(
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
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(request, CancellationToken);

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
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>()
            .ExecuteAsync(
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
        var show = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .Single();
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
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>()
            .ExecuteAsync(
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
        changed.UpdatedAt = changed.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        SetIds(library, [show]);
        library.TvShows.Add(show);

        // Act
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>()
            .ExecuteAsync(
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
        var otherShows = IDbContext
            .PlexTvShows.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .ToList();
        var otherSeasons = IDbContext
            .PlexTvShowSeason.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.TvShowId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .ToList();
        var otherEpisodes = IDbContext
            .PlexTvShowEpisodes.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.TvShowSeasonId,
                x.TvShowId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .ToList();
        var otherData = IDbContext
            .PlexTvShowEpisodeData.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTvShowEpisodeId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.Size,
            })
            .ToList();

        // Act
        var result = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(target))
        );

        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        IDbContext.PlexTvShows.Count(x => x.PlexLibraryId == target.Id).ShouldBe(0);
        IDbContext
            .PlexTvShows.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .ToList()
            .ShouldBe(otherShows);
        IDbContext
            .PlexTvShowSeason.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.TvShowId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .ToList()
            .ShouldBe(otherSeasons);
        IDbContext
            .PlexTvShowEpisodes.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.TvShowSeasonId,
                x.TvShowId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .ToList()
            .ShouldBe(otherEpisodes);
        IDbContext
            .PlexTvShowEpisodeData.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTvShowEpisodeId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.Size,
            })
            .ToList()
            .ShouldBe(otherData);
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
        var firstResult = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(command, CancellationToken);
        var secondShow = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .Single();
        SetIds(library, [secondShow]);
        library.TvShows.Clear();
        library.TvShows.Add(secondShow);
        var secondCommand = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        var secondResult = await Mock.Create<SyncPlexTvShowsCommandHandler>()
            .ExecuteAsync(secondCommand, CancellationToken);

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
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(command, CancellationToken);

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
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>()
            .ExecuteAsync(
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
    public async Task ShouldPreserveAncestorQualityIdentities_WhenIncomingHierarchyHasNoChanges()
    {
        // Arrange
        await SetupDatabase(
            11877,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var shows = await dbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var showQualitiesBefore = await dbContext
            .PlexTvShowMediaQualities.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTvShowId,
                x.VideoQuality,
            })
            .ToListAsync(CancellationToken);
        var seasonQualitiesBefore = await dbContext
            .PlexTvShowSeasonMediaQualities.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTvShowSeasonId,
                x.VideoQuality,
            })
            .ToListAsync(CancellationToken);
        var filesBefore = await dbContext
            .PlexTvShowEpisodeData.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTvShowEpisodeId,
                x.PlexApiPartId,
                x.Size,
                x.VideoQuality,
            })
            .ToListAsync(CancellationToken);
        showQualitiesBefore.Count.ShouldBe(2);
        seasonQualitiesBefore.Count.ShouldBe(2);
        filesBefore.Count.ShouldBe(2);
        SetIds(library, shows);
        library.TvShows.AddRange(shows);

        // Act
        var result = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.UpdatedTvShows.ShouldBe(0);
        result.Value.UpdatedSeasons.ShouldBe(0);
        result.Value.UpdatedEpisodes.ShouldBe(0);
        result.Value.UnchangedTvShows.ShouldBe(2);
        result.Value.UnchangedSeasons.ShouldBe(2);
        result.Value.UnchangedEpisodes.ShouldBe(2);
        (
            await dbContext
                .PlexTvShowMediaQualities.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexTvShowId,
                    x.VideoQuality,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(showQualitiesBefore);
        (
            await dbContext
                .PlexTvShowSeasonMediaQualities.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexTvShowSeasonId,
                    x.VideoQuality,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(seasonQualitiesBefore);
        (
            await dbContext
                .PlexTvShowEpisodeData.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexTvShowEpisodeId,
                    x.PlexApiPartId,
                    x.Size,
                    x.VideoQuality,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(filesBefore);
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

        var library = IDbContext.PlexLibraries.AsNoTracking().First();
        library.ShouldNotBeNull();
        var newTvShows = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .Where(x => x.PlexLibraryId == library.Id)
            .ToList();

        foreach (var x in newTvShows)
        {
            x.FullTitle = "TEST";
            x.UpdatedAt = x.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        }

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(request, CancellationToken);

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
        var tvShows = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToList();
        tvShows.ShouldNotBeNull();

        var newTvShows = tvShows.GetRange(0, 30);

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(request, CancellationToken);

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
        var newTvShows = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToList();

        foreach (var tvShow in newTvShows)
            tvShow.Seasons.AddRange(FakeData.GetPlexTvShowSeason(seed).Generate(3));

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(request, CancellationToken);

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
        var newTvShows = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToList();

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(request, CancellationToken);
        var secondShows = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToList();
        SetIds(library, secondShows);
        library.TvShows.Clear();
        library.TvShows.AddRange(secondShows);
        var secondRequest = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(secondRequest, CancellationToken)).IsValid.ShouldBeTrue();
        var result2 = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(secondRequest, CancellationToken);

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
        var tvShowsDb = IDbContext.PlexTvShows.AsNoTracking().ToList();
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

            newTvShows[i].Title = $"TEST - {newTvShows[i].Title}";
        }

        SetIds(library, newTvShows);
        library.TvShows.AddRange(newTvShows);

        // Act
        var request = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(request, CancellationToken);

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

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPersistStreamsForNewOrUpdatedEpisodeParts(bool isNew)
    {
        // Arrange
        var seed = await SetupDatabase(
            713461,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = isNew ? 0 : 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var show = isNew
            ? FakeData
                .GetPlexTvShows(
                    seed,
                    config =>
                    {
                        config.TvShowSeasonCount = 1;
                        config.TvShowEpisodeCount = 1;
                    }
                )
                .Generate()
            : IDbContext
                .PlexTvShows.AsNoTracking()
                .Include(x => x.Seasons)
                    .ThenInclude(x => x.Episodes)
                        .ThenInclude(x => x.MediaDataList)
                .Single();
        var episode = show.Seasons.Single().Episodes.Single();
        var mediaData = episode.MediaDataList.First();
        if (!isNew)
            episode.UpdatedAt = episode.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        mediaData.Streams.Add(
            new PlexTvShowEpisodeDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134611,
                StreamType = StreamType.Audio,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = null,
                Language = new StreamLanguage
                {
                    Id = 0,
                    LanguageCode = "fr",
                    LanguageTag = "fra",
                },
            }
        );
        SetIds(library, [show]);
        library.TvShows.Add(show);

        // Act
        var result = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        var persistedOwnerId = IDbContext
            .PlexTvShowEpisodeData.AsNoTracking()
            .Single(x => x.PlexTvShowEpisodeId == episode.Id)
            .Id;
        mediaData.Id.ShouldBe(persistedOwnerId);
        result.Value.CreatedEpisodes.ShouldBe(isNew ? 1 : 0);
        result.Value.UpdatedEpisodes.ShouldBe(isNew ? 0 : 1);
        var language = IDbContext.StreamLanguages.AsNoTracking().Single(x => x.LanguageCode == "fr");
        var persisted = IDbContext
            .PlexTvShowEpisodeDataStreams.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .ToList();
        persisted.ShouldBe([
            new
            {
                OwnerId = persistedOwnerId,
                SourceId = 7134611,
                Type = StreamType.Audio,
                LanguageId = language.Id,
                LanguageCode = "fr",
                LanguageTag = "fra",
                External = (bool?)null,
                Format = (string?)null,
            },
        ]);
    }

    [Test]
    public async Task ShouldLeaveEpisodeStreamsAndOwnersUnchanged_WhenTimestampIsUnchangedDespiteParentAndMediaDataChanges()
    {
        // Arrange
        await SetupDatabase(
            713471,
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
        var show = IDbContext
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .Single();
        var oldSeason = show.Seasons.OrderBy(x => x.Id).First();
        var newSeason = show.Seasons.OrderBy(x => x.Id).Last();
        var episode = oldSeason.Episodes.OrderBy(x => x.Id).First();
        var original = episode.MediaDataList.First();
        var added = FakeData.GetPlexTvShowEpisodeMediaData(new Seed(713472)).Generate();
        added.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiRatingKey), episode.PlexApiRatingKey);
        added.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiMediaId), original.PlexApiMediaId);
        original.Streams.Add(
            new PlexTvShowEpisodeDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134711,
                StreamType = StreamType.Audio,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = null,
                Language = new StreamLanguage
                {
                    Id = 0,
                    LanguageCode = "fr",
                    LanguageTag = "fra",
                },
            }
        );
        added.Streams.Add(
            new PlexTvShowEpisodeDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134712,
                StreamType = StreamType.Subtitle,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = "srt",
                Language = new StreamLanguage
                {
                    Id = 0,
                    LanguageCode = "en",
                    LanguageTag = "eng",
                },
            }
        );
        episode.MediaDataList.Add(added);
        var beforeEpisode = IDbContext
            .PlexTvShowEpisodes.AsNoTracking()
            .Where(x => x.Id == episode.Id)
            .Select(x => new
            {
                x.Id,
                x.TvShowId,
                x.TvShowSeasonId,
                x.ParentKey,
                x.UpdatedAt,
            })
            .Single();
        var beforeData = IDbContext
            .PlexTvShowEpisodeData.AsNoTracking()
            .Where(x => x.PlexTvShowEpisodeId == episode.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTvShowEpisodeId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
            })
            .OrderBy(x => x.Id)
            .ToList();
        var beforeStreams = IDbContext
            .PlexTvShowEpisodeDataStreams.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.MediaDataId,
                x.PlexApiStreamId,
                x.StreamType,
                x.LanguageId,
                x.IsExternal,
                x.SidecarFormat,
            })
            .OrderBy(x => x.Id)
            .ToList();

        var episodeId = episode.Id;
        oldSeason.Episodes.Remove(episode);
        episode.ParentKey = newSeason.PlexApiRatingKey;
        episode.ParentGuid = newSeason.Guid;
        newSeason.Episodes.Add(episode);
        SetIds(library, [show]);
        library.TvShows.Add(show);

        // Act
        var result = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        result.Value.UpdatedEpisodes.ShouldBe(0);
        result.Value.UnchangedEpisodes.ShouldBe(4);
        var afterEpisode = IDbContext
            .PlexTvShowEpisodes.AsNoTracking()
            .Where(x => x.Id == episodeId)
            .Select(x => new
            {
                x.Id,
                x.TvShowId,
                x.TvShowSeasonId,
                x.ParentKey,
                x.UpdatedAt,
            })
            .Single();
        afterEpisode.ShouldBe(beforeEpisode);
        IDbContext
            .PlexTvShowEpisodeData.AsNoTracking()
            .Where(x => x.PlexTvShowEpisodeId == episodeId)
            .Select(x => new
            {
                x.Id,
                x.PlexTvShowEpisodeId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
            })
            .OrderBy(x => x.Id)
            .ToList()
            .ShouldBe(beforeData);
        IDbContext
            .PlexTvShowEpisodeDataStreams.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.MediaDataId,
                x.PlexApiStreamId,
                x.StreamType,
                x.LanguageId,
                x.IsExternal,
                x.SidecarFormat,
            })
            .OrderBy(x => x.Id)
            .ToList()
            .ShouldBe(beforeStreams);
    }

    [Test]
    public async Task ShouldReconcileEpisodeStreamLifecycleWithoutChurn()
    {
        // Arrange
        await SetupDatabase(
            713465,
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

        async Task<PlexTvShow> LoadShow() =>
            await IDbContext
                .PlexTvShows.AsNoTracking()
                .Include(x => x.Seasons)
                    .ThenInclude(x => x.Episodes)
                        .ThenInclude(x => x.MediaDataList)
                .SingleAsync(CancellationToken);

        void SetIncoming(
            PlexTvShow show,
            bool includeStream,
            bool changed = false,
            bool updateTimestamp = false,
            bool differentSourceId = false
        )
        {
            var episode = show.Seasons.Single().Episodes.Single();
            var mediaData = episode.MediaDataList.First();
            mediaData.Streams.Clear();
            if (includeStream)
                mediaData.Streams.Add(
                    new PlexTvShowEpisodeDataStream
                    {
                        Id = 0,
                        MediaDataId = 0,
                        PlexApiStreamId = differentSourceId ? 7134652 : 7134651,
                        StreamType = StreamType.Audio,
                        LanguageId = 0,
                        IsExternal = changed,
                        SidecarFormat = null,
                        Language = new StreamLanguage
                        {
                            Id = 0,
                            LanguageCode = "fr",
                            LanguageTag = "fra",
                        },
                    }
                );
            if (updateTimestamp)
                episode.UpdatedAt = episode.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
            library.TvShows.Clear();
            SetIds(library, [show]);
            library.TvShows.Add(show);
        }

        var initial = await LoadShow();
        SetIncoming(initial, includeStream: true, updateTimestamp: true);

        // Act
        var initialResult = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        initialResult.IsSuccess.ShouldBeTrue(initialResult.ToString());
        initialResult.Errors.Count.ShouldBe(0);
        var persistedOwnerId = IDbContext
            .PlexTvShowEpisodeData.AsNoTracking()
            .Single(x => x.PlexTvShowEpisodeId == initial.Seasons.Single().Episodes.Single().Id)
            .Id;
        var language = IDbContext.StreamLanguages.AsNoTracking().Single(x => x.LanguageCode == "fr");
        var expectedInitial = new
        {
            OwnerId = persistedOwnerId,
            SourceId = 7134651,
            Type = StreamType.Audio,
            LanguageId = language.Id,
            LanguageCode = "fr",
            LanguageTag = "fra",
            External = (bool?)false,
            Format = (string?)null,
        };
        var firstId = IDbContext.PlexTvShowEpisodeDataStreams.AsNoTracking().Select(x => x.Id).Single();
        IDbContext
            .PlexTvShowEpisodeDataStreams.AsNoTracking()
            .Select(x => new
            {
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .Single()
            .ShouldBe(expectedInitial);

        var unchanged = await LoadShow();
        SetIncoming(unchanged, includeStream: true, changed: true, differentSourceId: true);

        // Act
        var unchangedResult = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        unchangedResult.IsSuccess.ShouldBeTrue(unchangedResult.ToString());
        unchangedResult.Errors.Count.ShouldBe(0);
        var unchangedStream = IDbContext
            .PlexTvShowEpisodeDataStreams.AsNoTracking()
            .Select(x => new
            {
                Id = x.Id,
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .Single();
        unchangedStream.Id.ShouldBe(firstId);
        new
        {
            OwnerId = unchangedStream.OwnerId,
            SourceId = unchangedStream.SourceId,
            Type = unchangedStream.Type,
            unchangedStream.LanguageId,
            unchangedStream.LanguageCode,
            unchangedStream.LanguageTag,
            unchangedStream.External,
            unchangedStream.Format,
        }.ShouldBe(expectedInitial);
        var unchangedEmpty = await LoadShow();
        SetIncoming(unchangedEmpty, includeStream: false);

        // Act
        var unchangedEmptyResult = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        unchangedEmptyResult.IsSuccess.ShouldBeTrue(unchangedEmptyResult.ToString());
        unchangedEmptyResult.Errors.Count.ShouldBe(0);
        unchangedEmptyResult.Value.UpdatedEpisodes.ShouldBe(0);
        IDbContext
            .PlexTvShowEpisodeDataStreams.AsNoTracking()
            .Select(x => new
            {
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .Single()
            .ShouldBe(expectedInitial);

        var changed = await LoadShow();
        SetIncoming(changed, includeStream: true, changed: true, updateTimestamp: true);

        // Act
        var changedResult = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        changedResult.IsSuccess.ShouldBeTrue(changedResult.ToString());
        changedResult.Errors.Count.ShouldBe(0);
        var changedOwnerId = IDbContext
            .PlexTvShowEpisodeData.AsNoTracking()
            .Single(x => x.PlexTvShowEpisodeId == changed.Seasons.Single().Episodes.Single().Id)
            .Id;
        var expectedChanged = new
        {
            OwnerId = changedOwnerId,
            SourceId = 7134651,
            Type = StreamType.Audio,
            LanguageId = language.Id,
            LanguageCode = "fr",
            LanguageTag = "fra",
            External = (bool?)true,
            Format = (string?)null,
        };
        var changedStream = IDbContext
            .PlexTvShowEpisodeDataStreams.AsNoTracking()
            .Select(x => new
            {
                Id = x.Id,
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .Single();
        changedStream.Id.ShouldBeGreaterThan(0);
        new
        {
            OwnerId = changedStream.OwnerId,
            SourceId = changedStream.SourceId,
            Type = changedStream.Type,
            changedStream.LanguageId,
            changedStream.LanguageCode,
            changedStream.LanguageTag,
            changedStream.External,
            changedStream.Format,
        }.ShouldBe(expectedChanged);

        // Arrange
        var empty = await LoadShow();
        var emptyEpisode = empty.Seasons.Single().Episodes.Single();
        var replacementMediaData = emptyEpisode.MediaDataList.Single();
        emptyEpisode.UpdatedAt = emptyEpisode.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        emptyEpisode.MediaDataList.Clear();
        library.TvShows.Clear();
        SetIds(library, [empty]);
        library.TvShows.Add(empty);

        // Act
        var emptyResult = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        emptyResult.IsSuccess.ShouldBeTrue(emptyResult.ToString());
        emptyResult.Errors.Count.ShouldBe(0);
        emptyResult.Value.UpdatedEpisodes.ShouldBe(1);
        IDbContext.PlexTvShowEpisodeDataStreams.AsNoTracking().ToList().ShouldBeEmpty();
        IDbContext
            .PlexTvShowEpisodeData.AsNoTracking()
            .Where(x => x.PlexTvShowEpisodeId == emptyEpisode.Id)
            .ToList()
            .ShouldBeEmpty();

        var forced = await LoadShow();
        var forcedEpisode = forced.Seasons.Single().Episodes.Single();
        forcedEpisode.MediaDataList.Add(replacementMediaData);
        SetIncoming(forced, includeStream: true);
        var forcedMediaData = forcedEpisode.MediaDataList.First();

        // Act
        var forcedResult = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library), ForceMediaRefresh: true)
        );

        // Assert
        forcedResult.IsSuccess.ShouldBeTrue(forcedResult.ToString());
        forcedResult.Errors.Count.ShouldBe(0);
        var forcedData = IDbContext
            .PlexTvShowEpisodeData.AsNoTracking()
            .Single(x =>
                x.PlexApiMediaId == forcedMediaData.PlexApiMediaId && x.PlexApiPartId == forcedMediaData.PlexApiPartId
            );
        var forcedStream = IDbContext
            .PlexTvShowEpisodeDataStreams.AsNoTracking()
            .Select(x => new
            {
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .Single();
        new
        {
            OwnerId = forcedStream.OwnerId,
            SourceId = forcedStream.SourceId,
            Type = forcedStream.Type,
            forcedStream.LanguageId,
            forcedStream.LanguageCode,
            forcedStream.LanguageTag,
            forcedStream.External,
            forcedStream.Format,
        }.ShouldBe(
            new
            {
                OwnerId = forcedData.Id,
                SourceId = 7134651,
                Type = StreamType.Audio,
                LanguageId = language.Id,
                LanguageCode = "fr",
                LanguageTag = "fra",
                External = (bool?)false,
                Format = (string?)null,
            }
        );
    }

    [Test]
    public async Task ShouldPreserveTvHierarchyAndStreamsWhenSynchronizationIsCancelled()
    {
        // Arrange
        await SetupDatabase(
            713476,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var db = IDbContext;
        var library = db.PlexLibraries.First();
        var show = db
            .PlexTvShows.AsNoTracking()
            .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .Single();
        var episode = show.Seasons.Single().Episodes.Single();
        episode
            .MediaDataList.First()
            .Streams.Add(
                new PlexTvShowEpisodeDataStream
                {
                    Id = 0,
                    MediaDataId = 0,
                    PlexApiStreamId = 7134761,
                    StreamType = StreamType.Audio,
                    LanguageId = 0,
                    IsExternal = null,
                    SidecarFormat = "aac",
                    Language = new StreamLanguage
                    {
                        Id = 0,
                        LanguageCode = "fr",
                        LanguageTag = "fra",
                    },
                }
            );
        SetIds(library, [show]);
        library.TvShows.Add(show);

        // Act
        var seeded = await TestHandlerExecuteAsync<BulkInsertTvShowsRapport>(
            new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        seeded.IsSuccess.ShouldBeTrue(seeded.ToString());
        seeded.Errors.Count.ShouldBe(0);
        var beforeShow = db
            .PlexTvShows.AsNoTracking()
            .Where(x => x.Id == show.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single();
        var beforeSeason = db
            .PlexTvShowSeason.AsNoTracking()
            .Where(x => x.TvShowId == show.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.TvShowId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single();
        var beforeEpisode = db
            .PlexTvShowEpisodes.AsNoTracking()
            .Where(x => x.TvShowSeasonId == beforeSeason.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.TvShowSeasonId,
                x.TvShowId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single();
        var beforeData = db
            .PlexTvShowEpisodeData.AsNoTracking()
            .Where(x => x.PlexTvShowEpisodeId == beforeEpisode.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTvShowEpisodeId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.Size,
            })
            .ToList();
        var beforeStreams = db
            .PlexTvShowEpisodeDataStreams.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .OrderBy(x => x.Id)
            .ToList();
        var beforeMetrics = db
            .PlexLibraries.AsNoTracking()
            .Where(x => x.Id == library.Id)
            .Select(x => new
            {
                x.MediaCount,
                x.TvShowCount,
                x.SeasonCount,
                x.EpisodeCount,
                x.EpisodeMediaDataCount,
                x.MediaSize,
            })
            .Single();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        SetIds(library, [show]);
        library.TvShows.Clear();
        library.TvShows.Add(show);
        var command = new SyncPlexTvShowsCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(command, CancellationToken)).IsValid.ShouldBeTrue();

        // Act
        var result = await Mock.Create<SyncPlexTvShowsCommandHandler>().ExecuteAsync(command, cancellation.Token);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        db.PlexTvShows.AsNoTracking()
            .Where(x => x.Id == beforeShow.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single()
            .ShouldBe(beforeShow);
        db.PlexTvShowSeason.AsNoTracking()
            .Where(x => x.Id == beforeSeason.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.TvShowId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single()
            .ShouldBe(beforeSeason);
        db.PlexTvShowEpisodes.AsNoTracking()
            .Where(x => x.Id == beforeEpisode.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.TvShowSeasonId,
                x.TvShowId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Title,
                x.MediaSize,
                x.UpdatedAt,
            })
            .Single()
            .ShouldBe(beforeEpisode);
        db.PlexTvShowEpisodeData.AsNoTracking()
            .Where(x => x.PlexTvShowEpisodeId == beforeEpisode.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexTvShowEpisodeId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.Size,
            })
            .ToList()
            .ShouldBe(beforeData);
        db.PlexTvShowEpisodeDataStreams.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                OwnerId = x.MediaDataId,
                SourceId = x.PlexApiStreamId,
                Type = x.StreamType,
                x.LanguageId,
                LanguageCode = x.Language.LanguageCode,
                LanguageTag = x.Language.LanguageTag,
                External = x.IsExternal,
                Format = x.SidecarFormat,
            })
            .OrderBy(x => x.Id)
            .ToList()
            .ShouldBe(beforeStreams);
        db.PlexLibraries.AsNoTracking()
            .Where(x => x.Id == library.Id)
            .Select(x => new
            {
                x.MediaCount,
                x.TvShowCount,
                x.SeasonCount,
                x.EpisodeCount,
                x.EpisodeMediaDataCount,
                x.MediaSize,
            })
            .Single()
            .ShouldBe(beforeMetrics);
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
            plexTvShow.Id = 0;
            plexTvShow.PlexLibraryId = library.Id;
            plexTvShow.PlexServerId = library.PlexServerId;
            foreach (var season in plexTvShow.Seasons)
            {
                season.Id = 0;
                season.TvShowId = 0;
                season.PlexLibraryId = library.Id;
                season.PlexServerId = library.PlexServerId;

                foreach (var episode in season.Episodes)
                {
                    episode.Id = 0;
                    episode.TvShowId = 0;
                    episode.TvShowSeasonId = 0;
                    episode.PlexLibraryId = library.Id;
                    episode.PlexServerId = library.PlexServerId;
                    foreach (var data in episode.MediaDataList)
                    {
                        data.Id = 0;
                        data.PlexTvShowEpisodeId = 0;
                        data.PlexLibraryId = library.Id;
                        data.PlexServerId = library.PlexServerId;
                        data.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiRatingKey), episode.PlexApiRatingKey);
                        foreach (var stream in data.Streams)
                        {
                            stream.Id = 0;
                            stream.UpdateInitProperty(nameof(BasePlexMediaDataStream.MediaDataId), 0);
                            stream.UpdateInitProperty(nameof(BasePlexMediaDataStream.LanguageId), 0);
                            stream.Language.Id = 0;
                        }
                    }
                }
            }
        }
    }
}
