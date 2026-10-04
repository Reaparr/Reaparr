using Reaparr.Domain.Validators;

namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskMoviesCommandHandlerUnitTests : BaseCommandUnitTest<GenerateDownloadTaskMoviesCommand>
{
    private readonly DownloadTaskMovieValidator _validator = new();

    [Test]
    public void GenerateDownloadTaskMoviesCommandValidator_ShouldRejectNullRequest()
    {
        // Arrange
        var validator = new GenerateDownloadTaskMoviesCommandValidator();
        var command = new GenerateDownloadTaskMoviesCommand((CreateDownloadTasksRequest)null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == nameof(GenerateDownloadTaskMoviesCommand.Request));
    }

    [Test]
    public async Task ShouldHaveInsertedValidDownloadTaskMoviesInDatabase_WhenGivenValidPlexMovies()
    {
        // Arrange
        await SetupDatabase(
            18022,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 5;
            }
        );

        var plexMovies = await IDbContext.PlexMovies.ToListAsync(CancellationToken);
        var movies = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Movie,
                MediaIds = plexMovies.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var command = new GenerateDownloadTaskMoviesCommand(movies);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Movies.ShouldBe(plexMovies.Count);
        var plexDownloadTaskMovies = await IDbContext.DownloadTaskMovie.IncludeAll().ToListAsync(CancellationToken);

        plexDownloadTaskMovies.Count.ShouldBe(5);
        plexDownloadTaskMovies
            .OrderBy(x => x.PlexApiRatingKey)
            .Select(x => (x.PlexApiRatingKey, x.PlexServerId, x.PlexLibraryId))
            .ShouldBe(
                plexMovies
                    .OrderBy(x => x.PlexApiRatingKey)
                    .Select(x => (x.PlexApiRatingKey, x.PlexServerId, x.PlexLibraryId))
            );

        foreach (var downloadTaskMovie in plexDownloadTaskMovies)
        {
            downloadTaskMovie.Calculate();
            var validationResult = await _validator.ValidateAsync(downloadTaskMovie, CancellationToken);

            // Ignore DownloadDirectory and DestinationDirectory errors as these are set in the DownloadJob
            var validErrors = validationResult.Errors.FindAll(x =>
                !x.PropertyName.Contains(nameof(DownloadTaskFileBase.DownloadDirectory))
                && !x.PropertyName.Contains(nameof(DownloadTaskFileBase.DestinationDirectory))
            );
            validErrors.ShouldBeEmpty();
        }
    }

    [Test]
    public async Task ShouldHaveDestinationFolderPathIdSet_WhenRequestContainsTheDestinationFolderPathIdSet()
    {
        // Arrange
        await SetupDatabase(
            18022,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 5;
            }
        );

        var plexMovies = await IDbContext.PlexMovies.ToListAsync(CancellationToken);
        var movies = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Movie,
                MediaIds = plexMovies.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var request = new CreateDownloadTasksRequest(movies, 99);
        var command = new GenerateDownloadTaskMoviesCommand(request);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Movies.ShouldBe(plexMovies.Count);
        var plexDownloadTaskMovies = await IDbContext.DownloadTaskMovie.IncludeAll().ToListAsync(CancellationToken);

        plexDownloadTaskMovies.Count.ShouldBe(5);
        plexDownloadTaskMovies
            .OrderBy(x => x.PlexApiRatingKey)
            .Select(x => (x.PlexApiRatingKey, x.PlexServerId, x.PlexLibraryId))
            .ShouldBe(
                plexMovies
                    .OrderBy(x => x.PlexApiRatingKey)
                    .Select(x => (x.PlexApiRatingKey, x.PlexServerId, x.PlexLibraryId))
            );

        foreach (var downloadTaskMovie in plexDownloadTaskMovies)
        {
            foreach (var child in downloadTaskMovie.Children)
            {
                child.DestinationFolderPathId.ShouldBe(99);
            }
        }
    }

    [Test]
    public async Task ShouldHaveMultipleDownloadTaskMovieFile_WhenPlexMovieHasMultiParts()
    {
        // Arrange
        await SetupDatabase(
            9999,
            config =>
            {
                config.MovieCount = 2;
                config.IncludeMultiPartMovies = true;
            }
        );

        var plexMovies = await IDbContext.PlexMovies.ToListAsync(CancellationToken);
        var movies = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Movie,
                MediaIds = plexMovies.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var command = new GenerateDownloadTaskMoviesCommand(movies);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Movies.ShouldBe(plexMovies.Count);

        var plexDownloadTaskMovies = await IDbContext
            .DownloadTaskMovie.IncludeAll()
            .Include(x => x.Children)
            .ToListAsync(CancellationToken);

        plexDownloadTaskMovies.Count.ShouldBe(2);
        plexDownloadTaskMovies
            .OrderBy(x => x.PlexApiRatingKey)
            .Select(x => (x.PlexApiRatingKey, x.PlexServerId, x.PlexLibraryId))
            .ShouldBe(
                plexMovies
                    .OrderBy(x => x.PlexApiRatingKey)
                    .Select(x => (x.PlexApiRatingKey, x.PlexServerId, x.PlexLibraryId))
            );

        foreach (var downloadTaskMovie in plexDownloadTaskMovies)
        {
            downloadTaskMovie.Calculate();
            var validationResult = await _validator.ValidateAsync(downloadTaskMovie, CancellationToken);

            // Ignore DownloadDirectory and DestinationDirectory errors as these are set in the DownloadJob
            var validErrors = validationResult.Errors.FindAll(x =>
                !x.PropertyName.Contains(nameof(DownloadTaskFileBase.DownloadDirectory))
                && !x.PropertyName.Contains(nameof(DownloadTaskFileBase.DestinationDirectory))
            );
            validErrors.ShouldBeEmpty();

            downloadTaskMovie.Children.Count.ShouldBe(2);
        }
    }

    [Test]
    public async Task ShouldKeepEachMovieBoundToItsOriginalLibrary_WhenGeneratingAcrossMultipleLibraries()
    {
        // Arrange
        await SetupDatabase(
            21123,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
                config.MovieCount = 6;
            }
        );

        var moviesByLibrary = await IDbContext
            .PlexMovies.GroupBy(x => x.PlexLibraryId)
            .Select(x => new { PlexLibraryId = x.Key, MovieId = x.OrderBy(y => y.Id).Select(y => y.Id).First() })
            .ToListAsync(CancellationToken);

        moviesByLibrary.Count.ShouldBeGreaterThanOrEqualTo(2);

        var movies = moviesByLibrary
            .Take(2)
            .Select(x => new DownloadMediaDTO
            {
                Type = PlexMediaType.Movie,
                MediaIds = [x.MovieId],
                PlexServerId = 1,
                PlexLibraryId = x.PlexLibraryId,
                Qualities = [],
            })
            .ToList();
        var selectedIds = movies.SelectMany(x => x.MediaIds).ToList();
        var expectedMovies = await IDbContext
            .PlexMovies.Where(x => selectedIds.Contains(x.Id))
            .ToListAsync(CancellationToken);

        // Act
        var command = new GenerateDownloadTaskMoviesCommand(movies);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Movies.ShouldBe(2);

        var downloadTaskMovies = await IDbContext.DownloadTaskMovie.ToListAsync(CancellationToken);

        downloadTaskMovies
            .OrderBy(x => x.PlexLibraryId)
            .Select(x => (x.PlexLibraryId, x.PlexApiRatingKey))
            .ShouldBe(expectedMovies.OrderBy(x => x.PlexLibraryId).Select(x => (x.PlexLibraryId, x.PlexApiRatingKey)));
    }

    [Test]
    [Arguments("server")]
    [Arguments("family")]
    [Arguments("foreign")]
    [Arguments("mixed")]
    [Arguments("later-group")]
    public async Task ShouldRejectSelectionWithoutPersistingTasks_WhenMovieScopeIsInconsistent(string mismatch)
    {
        // Arrange
        await SetupDatabase(
            64201,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        var dbContext = IDbContext;
        var sourceMovies = await dbContext.PlexMovies.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        sourceMovies.Count.ShouldBe(2);
        var target = sourceMovies[0];
        var foreign = sourceMovies[1];
        target.PlexServerId.ShouldNotBe(foreign.PlexServerId);
        target.PlexLibraryId.ShouldNotBe(foreign.PlexLibraryId);
        var musicLibrary = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.MusicArtist && x.PlexServerId == foreign.PlexServerId)
            .SingleAsync(CancellationToken);
        musicLibrary.Type.ShouldBe(PlexMediaType.MusicArtist);
        (await dbContext.DownloadTaskMovie.ToListAsync(CancellationToken)).ShouldBeEmpty();
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.Movie,
            PlexServerId = target.PlexServerId,
            PlexLibraryId = target.PlexLibraryId,
            MediaIds = [target.Id],
            Qualities = [],
        };
        var selections = mismatch switch
        {
            "server" => new List<DownloadMediaDTO> { selection with { PlexServerId = foreign.PlexServerId } },
            "family" => [selection with { PlexServerId = musicLibrary.PlexServerId, PlexLibraryId = musicLibrary.Id }],
            "foreign" =>
            [
                selection with
                {
                    PlexServerId = foreign.PlexServerId,
                    PlexLibraryId = foreign.PlexLibraryId,
                },
            ],
            "mixed" => [selection with { MediaIds = [target.Id, foreign.Id] }],
            "later-group" =>
            [
                selection,
                selection with
                {
                    PlexServerId = foreign.PlexServerId,
                    PlexLibraryId = foreign.PlexLibraryId,
                },
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMoviesCommand(selections)
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Has400BadRequestError().ShouldBeTrue();
        (await dbContext.DownloadTaskMovie.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.DownloadTaskMovieFile.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.DownloadTaskMovieFileLogs.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMovies.OrderBy(x => x.Id).ToListAsync(CancellationToken))
            .Select(x => (x.Id, x.PlexServerId, x.PlexLibraryId, x.PlexApiRatingKey))
            .ShouldBe(sourceMovies.Select(x => (x.Id, x.PlexServerId, x.PlexLibraryId, x.PlexApiRatingKey)));
    }
}
