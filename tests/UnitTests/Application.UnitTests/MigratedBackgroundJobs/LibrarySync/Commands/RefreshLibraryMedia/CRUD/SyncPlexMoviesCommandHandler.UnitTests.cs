namespace Reaparr.Application.UnitTests;

[NotInParallel]
public class SyncPlexMoviesCommandHandlerUnitTests : BaseCommandUnitTest<SyncPlexMoviesCommand>
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
        var result = await Mock.Create<SyncPlexMoviesCommandHandler>().ExecuteAsync(request, CancellationToken);

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
        var movies = IDbContext
            .PlexMovies.AsNoTracking()
            .Include(x => x.MediaDataList)
            .Where(x => x.PlexLibraryId == library.Id)
            .ToList();
        var idsByKey = movies.ToDictionary(x => x.PlexApiRatingKey, x => x.Id);
        SetIds(library, movies);
        library.Movies.AddRange(movies);

        // Act
        var result = await Mock.Create<SyncPlexMoviesCommandHandler>()
            .ExecuteAsync(
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
        var result = await Mock.Create<SyncPlexMoviesCommandHandler>()
            .ExecuteAsync(
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
        var changed = FakeData.GetPlexMovies(seed, x => x.IncludeMultiPartMovies = true).Generate();
        changed.PlexApiRatingKey = changedCurrent.PlexApiRatingKey;
        changed.UpdatedAt = changedCurrent.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        SetIds(library, [unchanged, changed]);
        library.Movies.Add(unchanged);
        library.Movies.Add(changed);

        // Act
        var result = await Mock.Create<SyncPlexMoviesCommandHandler>()
            .ExecuteAsync(
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
        IDbContext.PlexLibraries.Single(x => x.Id == library.Id).MovieMediaDataCount.ShouldBe(persistedMediaDataCount);
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
        var otherMovies = IDbContext
            .PlexMovies.AsNoTracking()
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
        var otherData = IDbContext
            .PlexMovieData.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexMovieId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.Size,
            })
            .ToList();

        // Act
        var result = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(target))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.DeletedMovies.ShouldBe(3);
        IDbContext.PlexMovies.Count(x => x.PlexLibraryId == target.Id).ShouldBe(0);
        IDbContext
            .PlexMovies.AsNoTracking()
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
            .ShouldBe(otherMovies);
        IDbContext
            .PlexMovieData.AsNoTracking()
            .Where(x => x.PlexLibraryId == other.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexMovieId,
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
        var firstResult = await Mock.Create<SyncPlexMoviesCommandHandler>().ExecuteAsync(command, CancellationToken);
        var persistedId = IDbContext.PlexMovies.AsNoTracking().Single().Id;
        var secondMovie = IDbContext.PlexMovies.AsNoTracking().Include(x => x.MediaDataList).Single();
        SetIds(library, [secondMovie]);
        library.Movies.Clear();
        library.Movies.Add(secondMovie);
        var secondCommand = new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library));
        var secondResult = await Mock.Create<SyncPlexMoviesCommandHandler>()
            .ExecuteAsync(secondCommand, CancellationToken);

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
        var result = await Mock.Create<SyncPlexMoviesCommandHandler>()
            .ExecuteAsync(
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
        SetIds(library, movies);
        library.Movies.AddRange(movies);
        var command = new SyncPlexMoviesCommand(
            new InsertMediaMetaDataCommandResponse(library),
            ForceMediaRefresh: true
        );

        // Act
        var result = await Mock.Create<SyncPlexMoviesCommandHandler>().ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var replacedMovies = IDbContext.PlexMovies.AsNoTracking().OrderBy(x => x.Id).ToList();
        replacedMovies.Count.ShouldBe(2);
        replacedMovies.Select(x => x.Id).ShouldAllBe(id => !originalIds.Contains(id));
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
        var moviesDb = IDbContext.PlexMovies.AsNoTracking().ToList();
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
        var result = await Mock.Create<SyncPlexMoviesCommandHandler>().ExecuteAsync(request, CancellationToken);

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
        var moviesDb = IDbContext.PlexMovies.AsNoTracking().Include(x => x.MediaDataList).ToList();
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
        var result = await Mock.Create<SyncPlexMoviesCommandHandler>().ExecuteAsync(request, CancellationToken);

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

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPersistStreamsForNewOrUpdatedMovieParts(bool isNew)
    {
        // Arrange
        var seed = await SetupDatabase(
            713460,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = isNew ? 0 : 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var movie = isNew
            ? FakeData.GetPlexMovies(seed).Generate()
            : IDbContext.PlexMovies.AsNoTracking().Include(x => x.MediaDataList).Single();
        var mediaData = movie.MediaDataList.First();
        if (!isNew)
            movie.UpdatedAt = movie.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        mediaData.Streams.Add(
            new PlexMovieDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134601,
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
        SetIds(library, [movie]);
        library.Movies.Add(movie);

        // Act
        var result = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        var persistedOwnerId = IDbContext.PlexMovieData.AsNoTracking().Single(x => x.PlexMovieId == movie.Id).Id;
        mediaData.Id.ShouldBe(persistedOwnerId);
        result.Value.CreatedMovies.ShouldBe(isNew ? 1 : 0);
        result.Value.UpdatedMovies.ShouldBe(isNew ? 0 : 1);
        var language = IDbContext.StreamLanguages.AsNoTracking().Single(x => x.LanguageCode == "en");
        var persisted = IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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
                SourceId = 7134601,
                Type = StreamType.Subtitle,
                LanguageId = language.Id,
                LanguageCode = "en",
                LanguageTag = "eng",
                External = (bool?)null,
                Format = (string?)"srt",
            },
        ]);
    }

    [Test]
    public async Task ShouldLeaveMovieStreamsAndOwnersUnchanged_WhenTimestampIsUnchangedDespiteMediaDataChanges()
    {
        // Arrange
        await SetupDatabase(
            713469,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var movie = IDbContext.PlexMovies.AsNoTracking().Include(x => x.MediaDataList).Single();
        var original = movie.MediaDataList.First();
        var added = FakeData.GetPlexMovieMediaData(new Seed(713470)).Generate();
        added.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiRatingKey), movie.PlexApiRatingKey);
        added.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiMediaId), original.PlexApiMediaId);
        original.Streams.Add(
            new PlexMovieDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134691,
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
        added.Streams.Add(
            new PlexMovieDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = 7134692,
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
        movie.MediaDataList.Add(added);
        var beforeData = IDbContext
            .PlexMovieData.AsNoTracking()
            .Where(x => x.PlexMovieId == movie.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexMovieId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
            })
            .OrderBy(x => x.Id)
            .ToList();
        var beforeStreams = IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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
        SetIds(library, [movie]);
        library.Movies.Add(movie);

        // Act
        var result = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        result.Value.UpdatedMovies.ShouldBe(0);
        result.Value.UnchangedMovies.ShouldBe(1);
        IDbContext
            .PlexMovieData.AsNoTracking()
            .Where(x => x.PlexMovieId == movie.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexMovieId,
                x.PlexLibraryId,
                x.PlexServerId,
                x.PlexApiMediaId,
                x.PlexApiPartId,
            })
            .OrderBy(x => x.Id)
            .ToList()
            .ShouldBe(beforeData);
        IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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
    public async Task ShouldReconcileMovieStreamLifecycleWithoutChurn()
    {
        // Arrange
        await SetupDatabase(
            713464,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();

        async Task<PlexMovie> LoadMovie() =>
            await IDbContext.PlexMovies.AsNoTracking().Include(x => x.MediaDataList).SingleAsync(CancellationToken);

        void SetIncoming(
            PlexMovie movie,
            bool includeStream,
            bool changed = false,
            bool updateTimestamp = false,
            bool differentSourceId = false
        )
        {
            var mediaData = movie.MediaDataList.First();
            mediaData.Streams.Clear();
            if (includeStream)
                mediaData.Streams.Add(
                    new PlexMovieDataStream
                    {
                        Id = 0,
                        MediaDataId = 0,
                        PlexApiStreamId = differentSourceId ? 7134642 : 7134641,
                        StreamType = StreamType.Subtitle,
                        LanguageId = 0,
                        IsExternal = changed,
                        SidecarFormat = changed ? "srt" : null,
                        Language = new StreamLanguage
                        {
                            Id = 0,
                            LanguageCode = "en",
                            LanguageTag = "eng",
                        },
                    }
                );
            if (updateTimestamp)
                movie.UpdatedAt = movie.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
            library.Movies.Clear();
            SetIds(library, [movie]);
            library.Movies.Add(movie);
        }

        var initial = await LoadMovie();
        SetIncoming(initial, includeStream: true, updateTimestamp: true);

        // Act
        var initialResult = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        initialResult.IsSuccess.ShouldBeTrue(initialResult.ToString());
        initialResult.Errors.Count.ShouldBe(0);
        var persistedOwnerId = IDbContext.PlexMovieData.AsNoTracking().Single(x => x.PlexMovieId == initial.Id).Id;
        var language = IDbContext.StreamLanguages.AsNoTracking().Single(x => x.LanguageCode == "en");
        var expectedInitial = new
        {
            OwnerId = persistedOwnerId,
            SourceId = 7134641,
            Type = StreamType.Subtitle,
            LanguageId = language.Id,
            LanguageCode = "en",
            LanguageTag = "eng",
            External = (bool?)false,
            Format = (string?)null,
        };
        var firstId = IDbContext.PlexMovieDataStreams.AsNoTracking().Select(x => x.Id).Single();
        IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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

        var unchanged = await LoadMovie();
        SetIncoming(unchanged, includeStream: true, changed: true, differentSourceId: true);

        // Act
        var unchangedResult = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        unchangedResult.IsSuccess.ShouldBeTrue(unchangedResult.ToString());
        unchangedResult.Errors.Count.ShouldBe(0);
        var unchangedStream = IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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
        var unchangedEmpty = await LoadMovie();
        SetIncoming(unchangedEmpty, includeStream: false);

        // Act
        var unchangedEmptyResult = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        unchangedEmptyResult.IsSuccess.ShouldBeTrue(unchangedEmptyResult.ToString());
        unchangedEmptyResult.Errors.Count.ShouldBe(0);
        unchangedEmptyResult.Value.UpdatedMovies.ShouldBe(0);
        IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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

        var changed = await LoadMovie();
        SetIncoming(changed, includeStream: true, changed: true, updateTimestamp: true);

        // Act
        var changedResult = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        changedResult.IsSuccess.ShouldBeTrue(changedResult.ToString());
        changedResult.Errors.Count.ShouldBe(0);
        var changedOwnerId = IDbContext.PlexMovieData.AsNoTracking().Single(x => x.PlexMovieId == changed.Id).Id;
        var expectedChanged = new
        {
            OwnerId = changedOwnerId,
            SourceId = 7134641,
            Type = StreamType.Subtitle,
            LanguageId = language.Id,
            LanguageCode = "en",
            LanguageTag = "eng",
            External = (bool?)true,
            Format = (string?)"srt",
        };
        var changedStream = IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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
        var empty = await LoadMovie();
        var replacementMediaData = empty.MediaDataList.Single();
        empty.UpdatedAt = empty.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        empty.MediaDataList.Clear();
        library.Movies.Clear();
        SetIds(library, [empty]);
        library.Movies.Add(empty);

        // Act
        var emptyResult = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        emptyResult.IsSuccess.ShouldBeTrue(emptyResult.ToString());
        emptyResult.Errors.Count.ShouldBe(0);
        emptyResult.Value.UpdatedMovies.ShouldBe(1);
        IDbContext.PlexMovieDataStreams.AsNoTracking().ToList().ShouldBeEmpty();
        IDbContext.PlexMovieData.AsNoTracking().Where(x => x.PlexMovieId == empty.Id).ToList().ShouldBeEmpty();

        var forced = await LoadMovie();
        forced.MediaDataList.Add(replacementMediaData);
        SetIncoming(forced, includeStream: true);
        var forcedMediaData = forced.MediaDataList.First();

        // Act
        var forcedResult = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library), ForceMediaRefresh: true)
        );

        // Assert
        forcedResult.IsSuccess.ShouldBeTrue(forcedResult.ToString());
        forcedResult.Errors.Count.ShouldBe(0);
        var forcedData = IDbContext
            .PlexMovieData.AsNoTracking()
            .Single(x =>
                x.PlexApiMediaId == forcedMediaData.PlexApiMediaId && x.PlexApiPartId == forcedMediaData.PlexApiPartId
            );
        var forcedStream = IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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
                SourceId = 7134641,
                Type = StreamType.Subtitle,
                LanguageId = language.Id,
                LanguageCode = "en",
                LanguageTag = "eng",
                External = (bool?)false,
                Format = (string?)null,
            }
        );
    }

    [Test]
    public async Task ShouldPreserveStreamsWhenSynchronizationIsCancelled()
    {
        // Arrange
        await SetupDatabase(
            713476,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        var movie = IDbContext.PlexMovies.AsNoTracking().Include(x => x.MediaDataList).Single();
        movie
            .MediaDataList.First()
            .Streams.Add(
                new PlexMovieDataStream
                {
                    Id = 0,
                    MediaDataId = 0,
                    PlexApiStreamId = 7134761,
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
        movie.UpdatedAt = movie.UpdatedAt?.AddSeconds(1) ?? DateTime.UtcNow;
        SetIds(library, [movie]);
        library.Movies.Add(movie);

        // Act
        var seeded = await TestHandlerExecuteAsync<CrudMoviesReport>(
            new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library))
        );

        // Assert
        seeded.IsSuccess.ShouldBeTrue(seeded.ToString());
        seeded.Errors.Count.ShouldBe(0);
        var before = IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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
            .Single();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        SetIds(library, [movie]);
        library.Movies.Clear();
        library.Movies.Add(movie);
        var command = new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library));
        (await _validator.ValidateAsync(command, CancellationToken)).IsValid.ShouldBeTrue();

        // Act
        var result = await Mock.Create<SyncPlexMoviesCommandHandler>().ExecuteAsync(command, cancellation.Token);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        IDbContext
            .PlexMovieDataStreams.AsNoTracking()
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
            .Single()
            .ShouldBe(before);
    }

    private void SetIds(PlexLibrary library, List<PlexMovie> newPlexMovies)
    {
        foreach (var plexMovie in newPlexMovies)
        {
            plexMovie.Id = 0;
            plexMovie.PlexLibraryId = library.Id;
            plexMovie.PlexServerId = library.PlexServerId;
            foreach (var data in plexMovie.MediaDataList)
            {
                data.Id = 0;
                data.PlexMovieId = 0;
                data.PlexLibraryId = library.Id;
                data.PlexServerId = library.PlexServerId;
                data.UpdateInitProperty(nameof(BasePlexMediaData.PlexApiRatingKey), plexMovie.PlexApiRatingKey);
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
