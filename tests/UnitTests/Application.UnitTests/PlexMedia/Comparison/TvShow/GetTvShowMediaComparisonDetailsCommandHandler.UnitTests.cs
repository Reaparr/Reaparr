namespace Reaparr.Application.UnitTests;

public class GetTvShowMediaComparisonDetailsCommandHandlerUnitTests
    : BaseCommandUnitTest<GetTvShowMediaComparisonDetailsCommand>
{
    [Test]
    public async Task ShouldReturnSeasonRowsWithSeasonPlexMediaIds_WhenRemoteTvShowHasMissingEpisodes()
    {
        // Arrange
        await SetupDatabase(
            63502,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 2, 10, 5, 14, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 8, 2, 9, 45, 2, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var remoteSeasons = await dbContext
            .PlexTvShowSeason.Where(x => x.TvShowId == remoteTvShow.Id)
            .OrderBy(x => x.SeasonNumber)
            .ToListAsync(CancellationToken);
        var remoteEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.TvShowId == remoteTvShow.Id)
            .OrderBy(x => x.TvShowSeasonId)
            .ThenBy(x => x.EpisodeNumber)
            .ToListAsync(CancellationToken);
        remoteSeasons.Count.ShouldBe(2);
        remoteEpisodes.Count.ShouldBe(4);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.TvShow);
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetTvShowMediaComparisonDetailsCommand(remoteTvShow.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.PlexMediaId.ShouldBe(remoteTvShow.Id);
        result.Value.Type.ShouldBe(PlexMediaType.TvShow);
        result.Value.State.ShouldBe(PlexMediaComparisonState.Missing);
        result.Value.Rows.Count.ShouldBe(2);
        foreach (var seasonRow in result.Value.Rows)
        {
            var expectedSeason = remoteSeasons.Single(x => x.Id == seasonRow.PlexMediaId);
            var expectedEpisodeIds = remoteEpisodes
                .Where(x => x.TvShowSeasonId == expectedSeason.Id)
                .Select(x => x.Id)
                .ToHashSet();

            seasonRow.PlexMediaId.ShouldBe(expectedSeason.Id);
            seasonRow.PlexMediaId.ShouldBeGreaterThan(0);
            seasonRow.Type.ShouldBe(PlexMediaType.Season);
            seasonRow.State.ShouldBe(PlexMediaComparisonState.Missing);
            seasonRow.PlexLibraryId.ShouldBe(remoteLibrary.Id);
            seasonRow.PlexServerId.ShouldBe(remoteLibrary.PlexServerId);
            seasonRow.Children.Count.ShouldBe(expectedEpisodeIds.Count);
            seasonRow.Children.Select(x => x.PlexMediaId).ShouldAllBe(x => expectedEpisodeIds.Contains(x));
            seasonRow.Children.ShouldAllBe(x => x.Type == PlexMediaType.Episode);
            seasonRow.Children.ShouldAllBe(x => x.State == PlexMediaComparisonState.Missing);
            seasonRow.Children.ShouldAllBe(x => x.Children.Count == 0);
        }
    }

    [Test]
    public async Task ShouldLoadEpisodesThroughTheirSeasons_WhenEpisodeTvShowRelationshipIsStale()
    {
        // Arrange
        await SetupDatabase(
            63503,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var ownedTvShow = await GetLibraryTvShowAsync(ownedLibrary.Id);
        var remoteSeasonId = await dbContext
            .PlexTvShowSeason.Where(x => x.PlexLibraryId == remoteLibrary.Id && x.TvShowId == remoteTvShow.Id)
            .Select(x => x.Id)
            .SingleAsync(CancellationToken);
        var episodeIds = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == remoteLibrary.Id && x.TvShowId == remoteTvShow.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        episodeIds.Count.ShouldBe(2);

        await dbContext
            .PlexTvShowEpisodes.Where(x => episodeIds.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.TvShowId, ownedTvShow.Id), CancellationToken);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.TvShow);
        await dbContext.SaveChangesAsync(CancellationToken);

        var staleEpisodeRelationships = await dbContext
            .PlexTvShowEpisodes.Where(x => episodeIds.Contains(x.Id))
            .Select(x => new { x.TvShowId, x.TvShowSeasonId })
            .ToListAsync(CancellationToken);
        staleEpisodeRelationships.ShouldAllBe(x => x.TvShowId == ownedTvShow.Id && x.TvShowSeasonId == remoteSeasonId);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(
            new GetTvShowMediaComparisonDetailsCommand(remoteTvShow.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.State.ShouldBe(PlexMediaComparisonState.Missing);
        result.Value.Rows.Count.ShouldBe(1);
        var seasonRow = result.Value.Rows.Single();
        seasonRow.PlexMediaId.ShouldBe(remoteSeasonId);
        seasonRow.Children.Count.ShouldBe(episodeIds.Count);
        seasonRow.Children.Select(x => x.PlexMediaId).ShouldAllBe(x => episodeIds.Contains(x));
        seasonRow.Children.Select(x => x.PlexMediaId).Distinct().Count().ShouldBe(episodeIds.Count);
        seasonRow.Children.ShouldAllBe(x => x.State == PlexMediaComparisonState.Missing);
    }

    [Test]
    public async Task ShouldNotReturnMissingRows_WhenRemoteTvShowMatchesAnyCurrentOwnedLibrary()
    {
        // Arrange
        await SetupDatabase(
            63512,
            config =>
            {
                config.PlexServerCount = 3;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var matchedOwnedLibrary = libraries[1];
        var missingOwnedLibrary = libraries[2];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(matchedOwnedLibrary.PlexServerId, true);
        await SetOwnedOverrideAsync(missingOwnedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 2, 10, 5, 14, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(matchedOwnedLibrary.Id, new DateTime(2026, 8, 2, 9, 45, 2, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(missingOwnedLibrary.Id, new DateTime(2026, 8, 2, 9, 50, 2, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        matchedOwnedLibrary = await GetLibraryAsync(matchedOwnedLibrary.Id);
        missingOwnedLibrary = await GetLibraryAsync(missingOwnedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var matchedOwnedTvShow = await GetLibraryTvShowAsync(matchedOwnedLibrary.Id);
        var remoteEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == remoteLibrary.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var matchedOwnedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == matchedOwnedLibrary.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        await AddCurrentScopeAsync(remoteLibrary, matchedOwnedLibrary, PlexMediaType.TvShow);
        await AddCurrentScopeAsync(remoteLibrary, missingOwnedLibrary, PlexMediaType.TvShow);
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(remoteLibrary.Id, matchedOwnedLibrary.Id, remoteTvShow.Id, matchedOwnedTvShow.Id)
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                matchedOwnedLibrary.Id,
                remoteEpisodes[0].Id,
                matchedOwnedEpisodes[0].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                matchedOwnedLibrary.Id,
                remoteEpisodes[1].Id,
                matchedOwnedEpisodes[1].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetTvShowMediaComparisonDetailsCommand(remoteTvShow.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.State.ShouldBe(PlexMediaComparisonState.Owned);
        result.Value.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldSelectEpisodeRowDeterministically_WhenHigherQualityHitsHaveEqualRemoteQualityAndLibraryId()
    {
        // Arrange
        await SetupDatabase(
            63504,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 2, 10, 5, 14, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 8, 2, 9, 45, 2, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var remoteEpisode = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == remoteLibrary.Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var ownedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == ownedLibrary.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.TvShow);
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisode.Id,
                ownedEpisodes[0].Id,
                PlexMediaComparisonHitState.HigherQuality,
                VideoQuality.HD
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisode.Id,
                ownedEpisodes[1].Id,
                PlexMediaComparisonHitState.HigherQuality,
                VideoQuality.SD
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetTvShowMediaComparisonDetailsCommand(remoteTvShow.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var episodeRow = result.Value.Rows.Single().Children.Single(x => x.PlexMediaId == remoteEpisode.Id);
        episodeRow.PlexMediaId.ShouldBe(remoteEpisode.Id);
        episodeRow.OwnedQuality.ShouldBe(VideoQuality.HD);
    }

    [Test]
    public async Task ShouldReturnOnlyHigherQualityEpisodeRows_WhenRemoteTvShowHasOwnedMatches()
    {
        // Arrange
        await SetupDatabase(
            63505,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 2, 10, 5, 14, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 8, 2, 9, 45, 2, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var remoteEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.TvShowId == remoteTvShow.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == ownedLibrary.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        remoteEpisodes.Count.ShouldBe(2);
        ownedEpisodes.Count.ShouldBe(2);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.TvShow);
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[0].Id,
                ownedEpisodes[0].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[1].Id,
                ownedEpisodes[1].Id,
                PlexMediaComparisonHitState.HigherQuality
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetTvShowMediaComparisonDetailsCommand(remoteTvShow.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.PlexMediaId.ShouldBe(remoteTvShow.Id);
        result.Value.Type.ShouldBe(PlexMediaType.TvShow);
        result.Value.State.ShouldBe(PlexMediaComparisonState.HigherQuality);
        result.Value.Rows.Count.ShouldBe(1);
        var seasonRow = result.Value.Rows.Single();
        seasonRow.Type.ShouldBe(PlexMediaType.Season);
        seasonRow.State.ShouldBe(PlexMediaComparisonState.HigherQuality);
        seasonRow.PlexLibraryId.ShouldBe(remoteLibrary.Id);
        seasonRow.PlexServerId.ShouldBe(remoteLibrary.PlexServerId);
        seasonRow.Children.Count.ShouldBe(1);
        var episodeRow = seasonRow.Children.Single();
        episodeRow.PlexMediaId.ShouldBe(remoteEpisodes[1].Id);
        episodeRow.Type.ShouldBe(PlexMediaType.Episode);
        episodeRow.State.ShouldBe(PlexMediaComparisonState.HigherQuality);
        episodeRow.RemoteQuality.ShouldBe(VideoQuality.FullHD);
        episodeRow.OwnedQuality.ShouldBe(VideoQuality.HD);
    }

    [Test]
    public async Task ShouldMergeKnownEpisodesAndKeepUnknownEpisodes_WhenOwnedTvShowMatchesMultipleRemoteServers()
    {
        // Arrange
        await SetupDatabase(
            63513,
            config =>
            {
                config.PlexServerCount = 3;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var firstRemoteLibrary = libraries[0];
        var secondRemoteLibrary = libraries[1];
        var ownedLibrary = libraries[2];
        await SetOwnedOverrideAsync(firstRemoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(secondRemoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(firstRemoteLibrary.Id, new DateTime(2026, 8, 2, 10, 5, 14, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(secondRemoteLibrary.Id, new DateTime(2026, 8, 2, 10, 6, 14, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 8, 2, 9, 45, 2, DateTimeKind.Utc));
        firstRemoteLibrary = await GetLibraryAsync(firstRemoteLibrary.Id);
        secondRemoteLibrary = await GetLibraryAsync(secondRemoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var firstRemoteTvShow = await GetLibraryTvShowAsync(firstRemoteLibrary.Id);
        var secondRemoteTvShow = await GetLibraryTvShowAsync(secondRemoteLibrary.Id);
        var ownedTvShow = await GetLibraryTvShowAsync(ownedLibrary.Id);
        var firstRemoteEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == firstRemoteLibrary.Id)
            .OrderBy(x => x.EpisodeNumber)
            .ToListAsync(CancellationToken);
        var secondRemoteEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == secondRemoteLibrary.Id)
            .OrderBy(x => x.EpisodeNumber)
            .ToListAsync(CancellationToken);
        var ownedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == ownedLibrary.Id)
            .OrderBy(x => x.EpisodeNumber)
            .ToListAsync(CancellationToken);

        await AddCurrentScopeAsync(firstRemoteLibrary, ownedLibrary, PlexMediaType.TvShow);
        await AddCurrentScopeAsync(secondRemoteLibrary, ownedLibrary, PlexMediaType.TvShow);
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(firstRemoteLibrary.Id, ownedLibrary.Id, firstRemoteTvShow.Id, ownedTvShow.Id)
        );
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(secondRemoteLibrary.Id, ownedLibrary.Id, secondRemoteTvShow.Id, ownedTvShow.Id)
        );
        firstRemoteEpisodes[1].EpisodeNumber = -1;
        secondRemoteEpisodes[1].EpisodeNumber = -1;
        dbContext.PlexTvShowEpisodes.UpdateRange(firstRemoteEpisodes[1], secondRemoteEpisodes[1]);
        for (var index = 0; index < ownedEpisodes.Count; index++)
        {
            dbContext.PlexEpisodeComparisons.Add(
                CreateEpisodeComparison(
                    firstRemoteLibrary.Id,
                    ownedLibrary.Id,
                    firstRemoteEpisodes[index].Id,
                    ownedEpisodes[index].Id,
                    PlexMediaComparisonHitState.HigherQuality
                )
            );
            dbContext.PlexEpisodeComparisons.Add(
                CreateEpisodeComparison(
                    secondRemoteLibrary.Id,
                    ownedLibrary.Id,
                    secondRemoteEpisodes[index].Id,
                    ownedEpisodes[index].Id,
                    PlexMediaComparisonHitState.HigherQuality
                )
            );
        }
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetTvShowMediaComparisonDetailsCommand(ownedTvShow.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.State.ShouldBe(PlexMediaComparisonState.HigherQuality);
        result.Value.Rows.Count.ShouldBe(1);
        var seasonRow = result.Value.Rows.Single();
        seasonRow.Title.ShouldBe("Season 1");
        seasonRow.Children.Count.ShouldBe(3);
        seasonRow.Children.ShouldContain(x => x.PlexMediaId == firstRemoteEpisodes[1].Id);
        seasonRow.Children.ShouldContain(x => x.PlexMediaId == secondRemoteEpisodes[1].Id);
    }

    [Test]
    public async Task ShouldReturnNoRows_WhenOwnedTvShowHasOnlyMatchedEpisodeHits()
    {
        // Arrange
        await SetupDatabase(
            63506,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 2, 10, 5, 14, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 8, 2, 9, 45, 2, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var ownedTvShow = await GetLibraryTvShowAsync(ownedLibrary.Id);
        var remoteEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == remoteLibrary.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == ownedLibrary.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.TvShow);
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(remoteLibrary.Id, ownedLibrary.Id, remoteTvShow.Id, ownedTvShow.Id)
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[0].Id,
                ownedEpisodes[0].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[1].Id,
                ownedEpisodes[1].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetTvShowMediaComparisonDetailsCommand(ownedTvShow.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.PlexMediaId.ShouldBe(ownedTvShow.Id);
        result.Value.Type.ShouldBe(PlexMediaType.TvShow);
        result.Value.State.ShouldBe(PlexMediaComparisonState.Owned);
        result.Value.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnFailure_WhenTvShowDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            63509,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
            }
        );

        var command = new GetTvShowMediaComparisonDetailsCommand(999_999);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains(nameof(PlexTvShow)));
    }

    [Test]
    public async Task ShouldReturnOwnedStateWithNoRows_WhenRemoteTvShowEpisodesAreAllMatched()
    {
        // Arrange
        await SetupDatabase(
            63510,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 2, 10, 5, 14, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 8, 2, 9, 45, 2, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var remoteEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == remoteLibrary.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == ownedLibrary.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.TvShow);
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[0].Id,
                ownedEpisodes[0].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[1].Id,
                ownedEpisodes[1].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetTvShowMediaComparisonDetailsCommand(remoteTvShow.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.PlexMediaId.ShouldBe(remoteTvShow.Id);
        result.Value.State.ShouldBe(PlexMediaComparisonState.Owned);
        result.Value.Rows.ShouldBeEmpty();
    }

}
