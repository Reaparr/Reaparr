namespace Reaparr.Application.UnitTests;

public class GetMovieMediaComparisonDetailsCommandHandlerUnitTests
    : BaseCommandUnitTest<GetMovieMediaComparisonDetailsCommand>
{
    [Test]
    public async Task ShouldReturnMissingMovieRow_WhenRemoteMovieHasCurrentOwnedScopeWithoutComparisonHits()
    {
        // Arrange
        await SetupDatabase(
            63501,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.Movie);
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetMovieMediaComparisonDetailsCommand(remoteMovie.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.PlexMediaId.ShouldBe(remoteMovie.Id);
        result.Value.Type.ShouldBe(PlexMediaType.Movie);
        result.Value.State.ShouldBe(PlexMediaComparisonState.Missing);
        result.Value.Rows.Count.ShouldBe(1);
        var row = result.Value.Rows.Single();
        row.PlexMediaId.ShouldBe(remoteMovie.Id);
        row.Type.ShouldBe(PlexMediaType.Movie);
        row.State.ShouldBe(PlexMediaComparisonState.Missing);
        row.PlexLibraryId.ShouldBe(remoteLibrary.Id);
        row.PlexServerId.ShouldBe(remoteLibrary.PlexServerId);
        row.Children.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldNotReturnMissingRows_WhenRemoteMovieMatchesAnyCurrentOwnedLibrary()
    {
        // Arrange
        await SetupDatabase(
            63511,
            config =>
            {
                config.PlexServerCount = 3;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 1;
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
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(matchedOwnedLibrary.Id, new DateTime(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(missingOwnedLibrary.Id, new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        matchedOwnedLibrary = await GetLibraryAsync(matchedOwnedLibrary.Id);
        missingOwnedLibrary = await GetLibraryAsync(missingOwnedLibrary.Id);
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(matchedOwnedLibrary.Id);

        await AddCurrentScopeAsync(remoteLibrary, matchedOwnedLibrary, PlexMediaType.Movie);
        await AddCurrentScopeAsync(remoteLibrary, missingOwnedLibrary, PlexMediaType.Movie);
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                matchedOwnedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetMovieMediaComparisonDetailsCommand(remoteMovie.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.State.ShouldBe(PlexMediaComparisonState.Owned);
        result.Value.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnHigherQualityRowsOnly_WhenRemoteMovieHasMixedCurrentHits()
    {
        // Arrange
        await SetupDatabase(
            63503,
            config =>
            {
                config.PlexServerCount = 3;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var matchedOwnedLibrary = libraries[1];
        var upgradeOwnedLibrary = libraries[2];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(matchedOwnedLibrary.PlexServerId, true);
        await SetOwnedOverrideAsync(upgradeOwnedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(matchedOwnedLibrary.Id, new DateTime(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(upgradeOwnedLibrary.Id, new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        matchedOwnedLibrary = await GetLibraryAsync(matchedOwnedLibrary.Id);
        upgradeOwnedLibrary = await GetLibraryAsync(upgradeOwnedLibrary.Id);
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var matchedOwnedMovie = await GetLibraryMovieAsync(matchedOwnedLibrary.Id);
        var upgradeOwnedMovie = await GetLibraryMovieAsync(upgradeOwnedLibrary.Id);

        await AddCurrentScopeAsync(remoteLibrary, matchedOwnedLibrary, PlexMediaType.Movie);
        await AddCurrentScopeAsync(remoteLibrary, upgradeOwnedLibrary, PlexMediaType.Movie);
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                matchedOwnedLibrary.Id,
                remoteMovie.Id,
                matchedOwnedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                upgradeOwnedLibrary.Id,
                remoteMovie.Id,
                upgradeOwnedMovie.Id,
                PlexMediaComparisonHitState.HigherQuality
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetMovieMediaComparisonDetailsCommand(remoteMovie.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.State.ShouldBe(PlexMediaComparisonState.HigherQuality);
        result.Value.Rows.Count.ShouldBe(1);
        var row = result.Value.Rows.Single();
        row.PlexMediaId.ShouldBe(remoteMovie.Id);
        row.Type.ShouldBe(PlexMediaType.Movie);
        row.State.ShouldBe(PlexMediaComparisonState.HigherQuality);
        row.PlexLibraryId.ShouldBe(remoteLibrary.Id);
        row.PlexServerId.ShouldBe(remoteLibrary.PlexServerId);
        row.RemoteQuality.ShouldBe(VideoQuality.FullHD);
        row.OwnedQuality.ShouldBe(VideoQuality.HD);
        row.Children.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnNoRows_WhenOwnedMovieHasOnlyMatchedRemoteHits()
    {
        // Arrange
        await SetupDatabase(
            63504,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.Movie);
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetMovieMediaComparisonDetailsCommand(ownedMovie.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.PlexMediaId.ShouldBe(ownedMovie.Id);
        result.Value.State.ShouldBe(PlexMediaComparisonState.Owned);
        result.Value.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnFailure_WhenMovieDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            63507,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 1;
            }
        );

        var command = new GetMovieMediaComparisonDetailsCommand(999_999);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains(nameof(PlexMovie)));
    }

    [Test]
    public async Task ShouldReturnOwnedStateWithNoRows_WhenRemoteMovieHasMatchedCurrentHitOnly()
    {
        // Arrange
        await SetupDatabase(
            63508,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.Movie);
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new GetMovieMediaComparisonDetailsCommand(remoteMovie.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.State.ShouldBe(PlexMediaComparisonState.Owned);
        result.Value.Rows.ShouldBeEmpty();
    }
}
