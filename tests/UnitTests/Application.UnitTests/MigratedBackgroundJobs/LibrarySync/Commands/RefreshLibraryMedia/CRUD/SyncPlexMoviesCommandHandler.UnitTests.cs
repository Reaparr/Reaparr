namespace Reaparr.Application.UnitTests;

[NotInParallel]
public class SyncPlexMoviesCommandHandlerUnitTests : BaseUnitTest<SyncPlexMoviesCommandHandler>
{
    private SyncPlexMoviesCommandValidator _validator = new();

    [Test]
    public async Task ShouldCreateAllMovies_WhenNoneExistsYet()
    {
        // Arrange
        var seed = await SetupDatabase(
            451253,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        library.ShouldNotBeNull();

        var movies = FakeData.GetPlexMovies(seed, x => x.IncludeMultiPartMovies = true).Generate(50);
        SetIds(library, movies);

        library.Movies.AddRange(movies);

        // Act
        var insertCommand = new InsertMediaMetaDataCommandResponse(library)
        {
            PlexCountries = [], // TODO add metadata here
        };
        var request = new SyncPlexMoviesCommand(insertCommand);
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var plexMoviesDb = IDbContext.PlexMovies.ToList();
        plexMoviesDb.Count.ShouldBe(50);
        var mediaDataCount = IDbContext.PlexMovieData.Count(x => x.PlexLibraryId == library.Id);
        mediaDataCount.ShouldBe(100);
        var persistedLibrary = IDbContext.PlexLibraries.Single(x => x.Id == library.Id);
        persistedLibrary.MovieCount.ShouldBe(50);
        persistedLibrary.MovieMediaDataCount.ShouldBe(mediaDataCount);
    }

    [Test]
    public async Task ShouldPreserveIdsAndReportNoChanges_WhenResultIsIdentical()
    {
        // Arrange
        await SetupDatabase(
            713452,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 10;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        var movies = IDbContext.PlexMovies.AsNoTracking().Where(x => x.PlexLibraryId == library.Id).ToList();
        var idsByKey = movies.ToDictionary(x => x.PlexApiRatingKey, x => x.Id);
        library.Movies.AddRange(movies);

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CreatedMovies.ShouldBe(0);
        result.Value.UpdatedMovies.ShouldBe(0);
        result.Value.DeletedMovies.ShouldBe(0);
        result.Value.UnchangedMovies.ShouldBe(10);
        result.Value.ToString().ShouldContain("UnchangedMovies: 10");
        foreach (var movie in IDbContext.PlexMovies.AsNoTracking())
            movie.Id.ShouldBe(idsByKey[movie.PlexApiRatingKey]);
    }

    [Test]
    public async Task ShouldDeleteAllMovies_WhenSuccessfulResultIsEmpty()
    {
        // Arrange
        await SetupDatabase(
            713453,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 10;
            }
        );

        var library = IDbContext.PlexLibraries.First();

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.DeletedMovies.ShouldBe(10);
        IDbContext.PlexMovies.Count(x => x.PlexLibraryId == library.Id).ShouldBe(0);
    }

    [Test]
    public async Task ShouldPreserveUnchangedNestedMediaAndReplaceOnlyChangedMovieMedia()
    {
        // Arrange
        var seed = await SetupDatabase(
            713454,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.IncludeMultiPartMovies = true;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        var currentMovies = IDbContext
            .PlexMovies.AsNoTracking()
            .Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToList();
        var unchanged = currentMovies[0];
        var changedCurrent = currentMovies[1];
        var unchangedId = unchanged.Id;
        var unchangedMediaIds = unchanged.MediaDataList.Select(x => x.Id).Order().ToList();
        var changedId = changedCurrent.Id;
        var oldChangedMediaIds = changedCurrent.MediaDataList.Select(x => x.Id).ToHashSet();
        unchanged.MediaDataList.Remove(unchanged.MediaDataList.First());
        var changed = FakeData.GetPlexMovies(seed, x => x.IncludeMultiPartMovies = true).Generate();
        changed.PlexApiRatingKey = changedCurrent.PlexApiRatingKey;
        changed.UpdatedAt = changedCurrent.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        SetIds(library, [unchanged, changed]);
        library.Movies.Add(unchanged);
        library.Movies.Add(changed);

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CreatedMovies.ShouldBe(0);
        result.Value.UpdatedMovies.ShouldBe(1);
        result.Value.DeletedMovies.ShouldBe(0);

        var persisted = IDbContext.PlexMovies.AsNoTracking().Include(x => x.MediaDataList).OrderBy(x => x.Id).ToList();
        var persistedUnchanged = persisted.Single(x => x.PlexApiRatingKey == unchanged.PlexApiRatingKey);
        var persistedChanged = persisted.Single(x => x.PlexApiRatingKey == changed.PlexApiRatingKey);
        persistedUnchanged.Id.ShouldBe(unchangedId);
        persistedUnchanged.MediaDataList.Select(x => x.Id).Order().ShouldBe(unchangedMediaIds);
        persistedChanged.Id.ShouldBe(changedId);
        persistedChanged.MediaDataList.Count.ShouldBe(2);
        persistedChanged.MediaDataList.ShouldAllBe(x => !oldChangedMediaIds.Contains(x.Id));
        var persistedMediaDataCount = persisted.Sum(x => x.MediaDataList.Count);
        IDbContext
            .PlexLibraries.Single(x => x.Id == library.Id)
            .MovieMediaDataCount.ShouldBe(persistedMediaDataCount);
    }

    [Test]
    public async Task ShouldNotDeleteMoviesFromOtherLibraries_WhenTargetResultIsEmpty()
    {
        // Arrange
        await SetupDatabase(
            713455,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
                config.MovieCount = 3;
            }
        );

        var libraries = IDbContext.PlexLibraries.OrderBy(x => x.Id).ToList();
        var target = libraries[0];
        var other = libraries[1];
        var otherIds = IDbContext.PlexMovies.Where(x => x.PlexLibraryId == other.Id).Select(x => x.Id).Order().ToList();

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(target)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.DeletedMovies.ShouldBe(3);
        IDbContext.PlexMovies.Count(x => x.PlexLibraryId == target.Id).ShouldBe(0);
        IDbContext.PlexMovies.Where(x => x.PlexLibraryId == other.Id).Select(x => x.Id).Order().ShouldBe(otherIds);
    }

    [Test]
    public async Task ShouldBeIdempotent_WhenSameChangedResultIsRepeated()
    {
        // Arrange
        var seed = await SetupDatabase(
            713456,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var current = IDbContext.PlexMovies.AsNoTracking().Single();
        var changed = FakeData.GetPlexMovies(seed).Generate();
        changed.PlexApiRatingKey = current.PlexApiRatingKey;
        changed.UpdatedAt = current.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        SetIds(library, [changed]);
        library.Movies.Add(changed);
        var command = new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library));

        // Act
        var firstResult = await Sut.ExecuteAsync(command, CancellationToken);
        var persistedId = IDbContext.PlexMovies.AsNoTracking().Single().Id;
        var secondResult = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        firstResult.IsSuccess.ShouldBeTrue();
        firstResult.Value.UpdatedMovies.ShouldBe(1);
        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Value.CreatedMovies.ShouldBe(0);
        secondResult.Value.UpdatedMovies.ShouldBe(0);
        secondResult.Value.DeletedMovies.ShouldBe(0);
        IDbContext.PlexMovies.AsNoTracking().Single().Id.ShouldBe(persistedId);
    }

    [Test]
    public async Task ShouldLeaveMoviesUnchanged_WhenAlreadyCancelled()
    {
        // Arrange
        await SetupDatabase(
            713457,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var originalIds = IDbContext.PlexMovies.Select(x => x.Id).Order().ToList();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var result = await Sut.ExecuteAsync(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library)),
            cancellationTokenSource.Token
        );

        // Assert
        result.IsCancelled.ShouldBeTrue();
        IDbContext.PlexMovies.Select(x => x.Id).Order().ShouldBe(originalIds);
    }

    [Test]
    public async Task ShouldReplaceAllMovies_WhenForceMediaRefreshIsTrue()
    {
        // Arrange
        await SetupDatabase(
            713459,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var movies = IDbContext.PlexMovies.AsNoTracking().OrderBy(x => x.Id).ToList();
        var originalIds = movies.Select(x => x.Id).ToList();
        library.Movies.AddRange(movies);
        var command = new SyncPlexMoviesCommand(
            new InsertMediaMetaDataCommandResponse(library),
            ForceMediaRefresh: true
        );

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var replacedMovies = IDbContext.PlexMovies.AsNoTracking().OrderBy(x => x.Id).ToList();
        replacedMovies.Count.ShouldBe(2);
        replacedMovies.Select(x => x.Id).ShouldAllBe(id => !originalIds.Contains(id));
    }

    [Test]
    public async Task ShouldSynchronizeRelationshipsForCompleteIncomingMovieList_WhenMoviesAreUnchanged()
    {
        // Arrange
        var seed = await SetupDatabase(
            713458,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var movies = IDbContext.PlexMovies.AsNoTracking().OrderBy(x => x.Id).ToList();
        var actor = FakeData.GetPlexActors(seed).Generate();
        await IDbContext.BulkInsertAsync([actor], cancellationToken: CancellationToken);
        actor.Id = IDbContext.PlexActors.AsNoTracking().Single(x => x.Key == actor.Key).Id;
        foreach (var movie in movies)
            movie.Actors.Add(actor);
        library.Movies.AddRange(movies);
        var metadata = new InsertMediaMetaDataCommandResponse(library)
        {
            PlexActors = new Dictionary<string, PlexActor> { [actor.Key] = actor },
        };

        // Act
        var result = await Sut.ExecuteAsync(new SyncPlexMoviesCommand(metadata), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UpdatedMovies.ShouldBe(0);
        var relations = IDbContext.PlexMovieActors.Where(x => x.PlexLibraryId == library.Id).ToList();
        relations.Count.ShouldBe(2);
        relations.Select(x => x.PlexMovieId).Order().ShouldBe(movies.Select(x => x.Id).Order());
        relations.ShouldAllBe(x => x.PlexActorId == actor.Id);
    }

    [Test]
    public async Task ShouldDeleteTwentyMovies_WhenSomeExist()
    {
        // Arrange
        await SetupDatabase(
            4503253,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 50;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        library.ShouldNotBeNull();
        var moviesDb = IDbContext.PlexMovies.ToList();
        moviesDb.ShouldNotBeNull();

        var movies = moviesDb.GetRange(0, 30);
        SetIds(library, movies);

        library.Movies.AddRange(movies);

        // Act
        var insertCommand = new InsertMediaMetaDataCommandResponse(library)
        {
            PlexCountries = [], // TODO add metadata here
        };
        var request = new SyncPlexMoviesCommand(insertCommand);
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var plexMoviesDb = IDbContext.PlexMovies.ToList();
        plexMoviesDb.Count.ShouldBe(30);
        foreach (var plexMovie in library.Movies)
            plexMoviesDb.Find(x => x.PlexApiRatingKey == plexMovie.PlexApiRatingKey).ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldCreateUpdateAndDeleteMovies_WhenSomeExist()
    {
        // Arrange
        var seed = await SetupDatabase(
            4903259,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 50;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        var moviesDb = IDbContext.PlexMovies.ToList();
        library.ShouldNotBeNull();
        var newMovies = new List<PlexMovie>();
        newMovies.AddRange(moviesDb.GetRange(0, 10));
        newMovies.AddRange(FakeData.GetPlexMovies(seed).Generate(30));

        for (var i = 0; i < newMovies.Count; i++)
        {
            // Create updated movies based on the Key
            if (i is >= 10 and < 30)
            {
                newMovies[i].PlexApiRatingKey = moviesDb[i].PlexApiRatingKey;
                newMovies[i].UpdatedAt = DateTime.UtcNow;
            }

            newMovies[i].Id = 0;
            newMovies[i].Title = $"TEST - {newMovies[i].Title}";
        }

        SetIds(library, newMovies);

        library.Movies.AddRange(newMovies);

        // Act
        var insertCommand = new InsertMediaMetaDataCommandResponse(library)
        {
            PlexCountries = [], // TODO add metadata here
        };
        var request = new SyncPlexMoviesCommand(insertCommand);
        (await _validator.ValidateAsync(request, CancellationToken)).IsValid.ShouldBeTrue();
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var plexMoviesDb = IDbContext.PlexMovies.ToList();
        plexMoviesDb.Count.ShouldBe(40);
        foreach (var plexMovie in newMovies)
        {
            var findResult = plexMoviesDb.Find(x => x.PlexApiRatingKey == plexMovie.PlexApiRatingKey);
            findResult.ShouldNotBeNull();
            if (plexMovie.UpdatedAt != moviesDb.Find(x => x.PlexApiRatingKey == plexMovie.PlexApiRatingKey)?.UpdatedAt)
                findResult.Title.Contains("TEST").ShouldBeTrue();
        }

        result.Value.CreatedMovies.ShouldBe(10);
        result.Value.UpdatedMovies.ShouldBe(20);
        result.Value.DeletedMovies.ShouldBe(20);
    }

    private void SetIds(PlexLibrary library, List<PlexMovie> newPlexMovies)
    {
        foreach (var plexMovie in newPlexMovies)
        {
            plexMovie.PlexLibraryId = library.Id;
            plexMovie.PlexServerId = library.PlexServerId;
        }
    }
}
