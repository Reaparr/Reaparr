namespace Reaparr.Application.UnitTests;

public class CompareMoviePlexLibraryCommandValidationUnitTests : BaseCommandUnitTest<CompareMoviePlexLibraryCommand>
{
    [Test]
    public async Task ShouldReturnFailure_WhenRemoteLibraryDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            76,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var ownedLibrary = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMoviePlexLibraryCommand(ownedLibrary.Id, 999_999));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains("Remote library 999999 was not found"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnFailure_WhenOwnedLibraryDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            77,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var remoteLibrary = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMoviePlexLibraryCommand(999_999, remoteLibrary.Id));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains("Owned library 999999 was not found"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnFailure_WhenRemoteLibraryIsOwned()
    {
        // Arrange
        await SetupDatabase(
            78,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, true);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains($"Library {remoteLibrary.Id} is owned"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnFailure_WhenOwnedLibraryIsRemote()
    {
        // Arrange
        await SetupDatabase(
            79,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, false);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains($"Library {ownedLibrary.Id} is not owned"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnFailure_WhenLibrariesAreNotBothMovieLibraries()
    {
        // Arrange
        await SetupDatabase(
            80,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var remoteMovieLibrary = await IDbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var ownedTvLibrary = await IDbContext
            .PlexLibraries.Where(x =>
                x.Type == PlexMediaType.TvShow && x.PlexServerId != remoteMovieLibrary.PlexServerId
            )
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        await SetOwnedOverrideAsync(remoteMovieLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedTvLibrary.PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedTvLibrary.Id, remoteMovieLibrary.Id)
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains("Both libraries must be movie libraries"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }
}

public class CompareTvShowPlexLibraryCommandValidationUnitTests : BaseCommandUnitTest<CompareTvShowPlexLibraryCommand>
{
    [Test]
    public async Task ShouldReturnFailure_WhenRemoteLibraryDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            81,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var ownedLibrary = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, 999_999));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains("Remote library 999999 was not found"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnFailure_WhenOwnedLibraryDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            82,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var remoteLibrary = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareTvShowPlexLibraryCommand(999_999, remoteLibrary.Id));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains("Owned library 999999 was not found"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnFailure_WhenRemoteLibraryIsOwned()
    {
        // Arrange
        await SetupDatabase(
            83,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, true);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains($"Library {remoteLibrary.Id} is owned"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnFailure_WhenOwnedLibraryIsRemote()
    {
        // Arrange
        await SetupDatabase(
            84,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, false);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains($"Library {ownedLibrary.Id} is not owned"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnFailure_WhenLibrariesAreNotBothTvShowLibraries()
    {
        // Arrange
        await SetupDatabase(
            85,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var remoteTvLibrary = await IDbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.TvShow)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var ownedMovieLibrary = await IDbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie && x.PlexServerId != remoteTvLibrary.PlexServerId)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        await SetOwnedOverrideAsync(remoteTvLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedMovieLibrary.PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedMovieLibrary.Id, remoteTvLibrary.Id)
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains("Both libraries must be TV show libraries"));
        var scopeCount = await IDbContext.PlexComparisonScopes.CountAsync(CancellationToken);
        scopeCount.ShouldBe(0);
    }
}
