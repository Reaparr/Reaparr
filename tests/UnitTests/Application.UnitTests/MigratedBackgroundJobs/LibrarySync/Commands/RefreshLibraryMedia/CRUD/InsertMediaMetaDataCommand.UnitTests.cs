namespace Reaparr.Application.UnitTests;

public class InsertMediaMetaDataCommandUnitTests : BaseCommandUnitTest<InsertMediaMetaDataCommand>
{
    [Test]
    public async Task ShouldSyncAllActorsGenresCountries_WhenNoneExist()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();
        var actors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(100, x => x.Key);
        var genres = FakePlexApiData.GetLibraryMediaItemGenreDTO(seed).GenerateUnique(100, x => x.Key);
        var countries = FakePlexApiData.GetLibraryMediaItemCountryDTO(seed).GenerateUnique(100, x => x.Key);
        var validActors = actors.Where(x => !string.IsNullOrEmpty(x.Key)).DistinctBy(x => x.Key).ToList();
        var validGenres = genres.Where(x => !string.IsNullOrEmpty(x.Key)).DistinctBy(x => x.Key).ToList();
        var validCountries = countries.Where(x => !string.IsNullOrEmpty(x.Key)).DistinctBy(x => x.Key).ToList();

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary)
            {
                Actors = actors,
                Genres = genres,
                Countries = countries,
            }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        actorsDb.Count.ShouldBe(validActors.Count);
        foreach (var actor in validActors)
            actorsDb.ShouldContain(
                x => x.Name == actor.Name && x.Key == actor.Key,
                $"Actor {actor.Name} with key {actor.Key} not found in database"
            );

        var genresDb = await IDbContext.PlexGenres.ToListAsync(CancellationToken);
        genresDb.Count.ShouldBe(validGenres.Count);
        foreach (var genre in validGenres)
            genresDb.ShouldContain(x => x.Key == genre.Key, $"Genre {genre.Name} not found in database");

        var countriesDb = await IDbContext.PlexCountries.ToListAsync(CancellationToken);
        countriesDb.Count.ShouldBe(validCountries.Count);
        foreach (var country in validCountries)
            countriesDb.ShouldContain(x => x.Key == country.Key, $"Country {country.Name} not found in database");
    }

    [Test]
    public async Task ShouldPersistGenreType_WhenGenreIsInserted()
    {
        // Arrange
        await SetupDatabase(1224, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
        });
        var plexLibrary = await IDbContext.PlexLibraries.FirstAsync(CancellationToken);
        var genre = new LibraryMediaItemGenreDTO
        {
            PlexId = 1,
            Name = "Documentaire",
            Filter = "documentaire",
            Key = "documentaire",
        };
        var command = new InsertMediaMetaDataCommand(
            new LibraryMetadata(plexLibrary) { Genres = [genre] }
        );

        // Act
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await IDbContext.PlexGenres.SingleAsync(CancellationToken)).Type.ShouldBe(PlexGenreType.Documentary);
    }

    [Test]
    public async Task ShouldUpdateExistingActorsGenresCountries_WhenSomeExist()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Create initial data
        var initialActors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(50, x => x.Key);
        var initialGenres = FakePlexApiData.GetLibraryMediaItemGenreDTO(seed).GenerateUnique(50, x => x.Key);
        var initialCountries = FakePlexApiData.GetLibraryMediaItemCountryDTO(seed).GenerateUnique(50, x => x.Key);

        // Insert initial data
        var dbContext = IDbContext;
        await dbContext.PlexActors.AddRangeAsync(initialActors.ToPlexActor(), CancellationToken);
        await dbContext.PlexGenres.AddRangeAsync(initialGenres.ToPlexGenre(), CancellationToken);
        await dbContext.PlexCountries.AddRangeAsync(initialCountries.ToPlexCountry(), CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Create new data with some overlaps
        var newActors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(100, x => x.Key);
        var newGenres = FakePlexApiData.GetLibraryMediaItemGenreDTO(seed).GenerateUnique(100, x => x.Key);
        var newCountries = FakePlexApiData.GetLibraryMediaItemCountryDTO(seed).GenerateUnique(100, x => x.Key);

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary)
            {
                Actors = newActors,
                Genres = newGenres,
                Countries = newCountries,
            }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        var expectedActorCount = initialActors
            .Select(x => x.Key)
            .Concat(newActors.Select(x => x.Key))
            .Distinct()
            .Count();
        actorsDb.Count.ShouldBe(expectedActorCount);
        foreach (var actor in newActors)
            actorsDb.ShouldContain(x => x.Name == actor.Name && x.Key == actor.Key);

        var genresDb = await IDbContext.PlexGenres.ToListAsync(CancellationToken);
        var expectedGenreCount = initialGenres
            .Select(x => x.Key)
            .Concat(newGenres.Select(x => x.Key))
            .Distinct()
            .Count();
        genresDb.Count.ShouldBe(expectedGenreCount);
        foreach (var genre in newGenres)
            genresDb.ShouldContain(x => x.Key == genre.Key);

        var countriesDb = await IDbContext.PlexCountries.ToListAsync(CancellationToken);
        var expectedCountryCount = initialCountries
            .Select(x => x.Key)
            .Concat(newCountries.Select(x => x.Key))
            .Distinct()
            .Count();
        countriesDb.Count.ShouldBe(expectedCountryCount);
        foreach (var country in newCountries)
            countriesDb.ShouldContain(x => x.Key == country.Key);
    }

    [Test]
    public async Task ShouldHandleEmptyLists_WhenNoDataProvided()
    {
        // Arrange
        await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Act
        var command = new InsertMediaMetaDataCommand(LibraryMetadata: new LibraryMetadata(plexLibrary));
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        actorsDb.ShouldBeEmpty();

        var genresDb = await IDbContext.PlexGenres.ToListAsync(CancellationToken);
        genresDb.ShouldBeEmpty();

        var countriesDb = await IDbContext.PlexCountries.ToListAsync(CancellationToken);
        countriesDb.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldHandleDuplicatePlexKeys_WhenDataContainsDuplicates()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Create data with duplicate PlexKeys
        var plexApiActors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).Generate(10);
        var duplicateActors = plexApiActors.Select(r => r with { Name = r.Name + "_duplicate" }).ToList();
        plexApiActors.AddRange(duplicateActors);

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary) { Actors = plexApiActors }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        actorsDb.Count.ShouldBe(10); // Should only have unique PlexKeys
        actorsDb.Select(x => x.Key).Distinct().Count().ShouldBe(10);
    }

    [Test]
    public async Task ShouldHandleConcurrentSyncs_WhenMultipleOperationsRun()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        var actors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(100, x => x.Key);

        // Act
        var command1 = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary) { Actors = actors.Slice(0, 50) }
        );

        var command2 = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary) { Actors = actors.Slice(50, 50) }
        );

        var result1 = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command1);
        var result2 = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command2);

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        actorsDb.Count.ShouldBe(100);
        actorsDb.Select(x => x.Key).Distinct().Count().ShouldBe(100);
    }

    [Test]
    public async Task ShouldHandleLargeDataSet_WhenManyItemsProvided()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Create a large dataset
        var actors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).Generate(250);
        var genres = FakePlexApiData.GetLibraryMediaItemGenreDTO(seed).Generate(250);
        var countries = FakePlexApiData.GetLibraryMediaItemCountryDTO(seed).Generate(250);

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary)
            {
                Actors = actors,
                Genres = genres,
                Countries = countries,
            }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        var genresDb = await IDbContext.PlexGenres.ToListAsync(CancellationToken);
        var countriesDb = await IDbContext.PlexCountries.ToListAsync(CancellationToken);

        var expectedActorCount = actors.Select(x => x.Key).Where(x => !string.IsNullOrEmpty(x)).Distinct().Count();
        actorsDb.Count.ShouldBe(expectedActorCount);
        var expectedGenreCount = genres.Select(x => x.Key).Where(x => !string.IsNullOrEmpty(x)).Distinct().Count();
        genresDb.Count.ShouldBe(expectedGenreCount);
        var expectedCountryCount = countries.Select(x => x.Key).Where(x => !string.IsNullOrEmpty(x)).Distinct().Count();
        countriesDb.Count.ShouldBe(expectedCountryCount);
    }

    [Test]
    public async Task ShouldPreserveDataIntegrity_WhenUpdatingExistingItems()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Create initial data
        var initialActors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(50, x => x.Key);
        await IDbContext.PlexActors.AddRangeAsync(initialActors.ToPlexActor(), CancellationToken);
        await IDbContext.SaveChangesAsync(CancellationToken);

        // Create new data with some overlapping PlexKeys but different names
        var newActors = initialActors.Take(25).Select(r => r with { Name = r.Name + "_updated" }).ToList();
        var actorFaker = FakePlexApiData.GetLibraryMediaItemActorDTO(seed);
        var actorKeys = initialActors.Select(x => x.Key).ToHashSet();
        while (newActors.Count < 50)
        {
            var actor = actorFaker.Generate();
            if (string.IsNullOrEmpty(actor.Key) || !actorKeys.Add(actor.Key))
                continue;

            newActors.Add(actor);
        }

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary) { Actors = newActors }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        actorsDb.Count.ShouldBe(50);

        // Verify that existing items were updated
        foreach (var actor in newActors.Take(25))
        {
            var dbActor = actorsDb.FirstOrDefault(x => x.Key == actor.Key);
            dbActor.ShouldNotBeNull();
            dbActor.Name.ShouldBe(actor.Name);
        }
    }

    [Test]
    public async Task ShouldHandleNullLibraryMetadata_WhenProvided()
    {
        // Arrange
        await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Act
        var command = new InsertMediaMetaDataCommand(LibraryMetadata: null!);
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldFilterOutActorsWithEmptyKey_WhenActorsHaveEmptyKeys()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Create actors with some having null TagKey
        var actorsWithKeys = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(50, x => x.Key);
        var actorsWithNullKeys = FakePlexApiData
            .GetLibraryMediaItemActorDTO(seed)
            .GenerateUnique(30, x => x.Name)
            .Select(x => x with { Key = string.Empty })
            .ToList();

        var allActors = actorsWithKeys.Concat(actorsWithNullKeys).ToList();

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary) { Actors = allActors }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);

        // Only actors with Tag should be inserted
        actorsDb.Count.ShouldBe(50);
        actorsDb.ShouldAllBe(x => x.Key != string.Empty);
    }

    [Test]
    public async Task ShouldHandlePartiallyEmptyMetadata_WhenOnlySomeListsAreEmpty()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        var actors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(25, x => x.Key);

        // Genres and Countries are empty

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary) { Actors = actors }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.PlexActors.Count.ShouldBe(25);
        result.Value.PlexGenres.ShouldBeEmpty();
        result.Value.PlexCountries.ShouldBeEmpty();

        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        var genresDb = await IDbContext.PlexGenres.ToListAsync(CancellationToken);
        var countriesDb = await IDbContext.PlexCountries.ToListAsync(CancellationToken);

        actorsDb.Count.ShouldBe(25);
        genresDb.ShouldBeEmpty();
        countriesDb.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnCorrectDictionaryMappings_WhenDataIsInserted()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        var actors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(10, x => x.Key);
        var genres = FakePlexApiData.GetLibraryMediaItemGenreDTO(seed).GenerateUnique(10, x => x.Key);
        var countries = FakePlexApiData.GetLibraryMediaItemCountryDTO(seed).GenerateUnique(10, x => x.Key);

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary)
            {
                Actors = actors,
                Genres = genres,
                Countries = countries,
            }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify dictionary keys match the source PlexIds
        foreach (var actor in actors)
        {
            result.Value.PlexActors.ShouldContainKey(actor.Key);
            result.Value.PlexActors[actor.Key].Name.ShouldBe(actor.Name);
        }

        foreach (var genre in genres)
        {
            result.Value.PlexGenres.ShouldContainKey(genre.Key);
            result.Value.PlexGenres[genre.Key].Key.ShouldBe(genre.Key);
        }

        foreach (var country in countries)
        {
            result.Value.PlexCountries.ShouldContainKey(country.Key);
            result.Value.PlexCountries[country.Key].Key.ShouldBe(country.Key);
        }
    }

    [Test]
    public async Task ShouldHandleSpecialCharactersInNames_WhenDataContainsUnicodeCharacters()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Create actors with special characters
        var baseActors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).Generate(5);
        var specialActors = baseActors
            .Select(
                (actor, index) =>
                    actor with
                    {
                        Name = index switch
                        {
                            0 => "José María Aznar",
                            1 => "André François",
                            2 => "张三丰",
                            3 => "محمد عبدالله",
                            4 => "Björk Guðmundsdóttir",
                            _ => actor.Name,
                        },
                    }
            )
            .ToList();

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary) { Actors = specialActors }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        actorsDb.Count.ShouldBe(5);

        actorsDb.ShouldContain(x => x.Name == "José María Aznar");
        actorsDb.ShouldContain(x => x.Name == "André François");
        actorsDb.ShouldContain(x => x.Name == "张三丰");
        actorsDb.ShouldContain(x => x.Name == "محمد عبدالله");
        actorsDb.ShouldContain(x => x.Name == "Björk Guðmundsdóttir");
    }

    [Test]
    public async Task ShouldMaintainConsistency_WhenSameDataInsertedMultipleTimes()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        var actors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(20, x => x.Key);
        var genres = FakePlexApiData.GetLibraryMediaItemGenreDTO(seed).GenerateUnique(20, x => x.Key);
        var countries = FakePlexApiData.GetLibraryMediaItemCountryDTO(seed).GenerateUnique(20, x => x.Key);

        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary)
            {
                Actors = actors,
                Genres = genres,
                Countries = countries,
            }
        );

        // Act - Insert the same data multiple times
        var result1 = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);
        var result2 = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);
        var result3 = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        result3.IsSuccess.ShouldBeTrue();

        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        var genresDb = await IDbContext.PlexGenres.ToListAsync(CancellationToken);
        var countriesDb = await IDbContext.PlexCountries.ToListAsync(CancellationToken);

        // Should not have duplicates
        actorsDb.Count.ShouldBe(20);
        actorsDb.Select(x => x.Key).Distinct().Count().ShouldBe(20);

        genresDb.Select(x => x.Key).Distinct().Count().ShouldBe(genresDb.Count);
        countriesDb.Select(x => x.Key).Distinct().Count().ShouldBe(countriesDb.Count);
    }

    [Test]
    public async Task ShouldCorrectlyMapLibraryReference_WhenLibraryIsProvided()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        var actors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).Generate(5);

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary) { Actors = actors }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.PlexLibrary.ShouldBe(plexLibrary);
        result.Value.PlexLibraryId.ShouldBe(plexLibrary.Id);
    }

    [Test]
    public async Task ShouldHandleVeryLongNames_WhenDataContainsLongStrings()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Create actors with very long names
        var baseActors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(3, x => x.Key);
        var longNameActors = baseActors
            .Select(
                (actor, index) =>
                    actor with
                    {
                        // Very long name
                        Name = new string('A', 250) + index,
                    }
            )
            .ToList();

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary) { Actors = longNameActors }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        actorsDb.Count.ShouldBe(3);
        actorsDb.ShouldAllBe(x => x.Name.Length >= 250);
    }

    [Test]
    public async Task ShouldReturnEmptyDictionaries_WhenAllListsAreEmpty()
    {
        // Arrange
        await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Act
        var command = new InsertMediaMetaDataCommand(LibraryMetadata: new LibraryMetadata(plexLibrary));
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.PlexActors.ShouldBeEmpty();
        result.Value.PlexGenres.ShouldBeEmpty();
        result.Value.PlexCountries.ShouldBeEmpty();
        result.Value.PlexLibrary.ShouldBe(plexLibrary);
    }

    [Test]
    public async Task ShouldHandleZeroKeys_WhenDataContainsZeroValues()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        // Create data with zero keys
        var baseData = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).Generate(5);
        var actorsWithZeroKeys = baseData.Select(x => x with { Key = string.Empty }).ToList();

        var baseGenres = FakePlexApiData.GetLibraryMediaItemGenreDTO(seed).Generate(3);
        var genresWithZeroKeys = baseGenres.Select(x => x with { Key = string.Empty }).ToList();

        var baseCountries = FakePlexApiData.GetLibraryMediaItemCountryDTO(seed).Generate(3);
        var countriesWithZeroKeys = baseCountries.Select(x => x with { Key = string.Empty }).ToList();

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary)
            {
                Actors = actorsWithZeroKeys,
                Genres = genresWithZeroKeys,
                Countries = countriesWithZeroKeys,
            }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        var genresDb = await IDbContext.PlexGenres.ToListAsync(CancellationToken);
        var countriesDb = await IDbContext.PlexCountries.ToListAsync(CancellationToken);

        actorsDb.Count.ShouldBe(0);
        genresDb.Count.ShouldBe(0);
        countriesDb.Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnValidDatabaseIds_WhenEntitiesAreInserted()
    {
        // Arrange
        var seed = await SetupDatabase(
            1223,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        var actors = FakePlexApiData.GetLibraryMediaItemActorDTO(seed).GenerateUnique(15, x => x.Key);
        var genres = FakePlexApiData.GetLibraryMediaItemGenreDTO(seed).GenerateUnique(12, x => x.Key);
        var countries = FakePlexApiData.GetLibraryMediaItemCountryDTO(seed).GenerateUnique(8, x => x.Key);

        // Act
        var command = new InsertMediaMetaDataCommand(
            LibraryMetadata: new LibraryMetadata(plexLibrary)
            {
                Actors = actors,
                Genres = genres,
                Countries = countries,
            }
        );
        var result = await TestHandlerExecuteAsync<InsertMediaMetaDataCommandResponse>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify dictionary keys are valid PlexIds
        result.Value.PlexActors.Keys.ShouldAllBe(
            key => !string.IsNullOrEmpty(key),
            "All actor PlexIds should be positive"
        );
        result.Value.PlexGenres.Keys.ShouldAllBe(
            key => !string.IsNullOrEmpty(key),
            "All genre PlexIds should be positive"
        );
        result.Value.PlexCountries.Keys.ShouldAllBe(
            key => !string.IsNullOrEmpty(key),
            "All country PlexIds should be positive"
        );

        // Verify entities have valid database IDs
        result.Value.PlexActors.Values.ShouldAllBe(actor => actor.Id > 0, "All actors should have valid database IDs");
        result.Value.PlexGenres.Values.ShouldAllBe(genre => genre.Id > 0, "All genres should have valid database IDs");
        result.Value.PlexCountries.Values.ShouldAllBe(
            country => country.Id > 0,
            "All countries should have valid database IDs"
        );

        // Verify database entities match returned entities
        var actorsDb = await IDbContext.PlexActors.ToListAsync(CancellationToken);
        var genresDb = await IDbContext.PlexGenres.ToListAsync(CancellationToken);
        var countriesDb = await IDbContext.PlexCountries.ToListAsync(CancellationToken);

        actorsDb.Count.ShouldBe(result.Value.PlexActors.Count);
        genresDb.Count.ShouldBe(result.Value.PlexGenres.Count);
        countriesDb.Count.ShouldBe(result.Value.PlexCountries.Count);

        // Verify each returned entity exists in the database
        foreach (var returnedActor in result.Value.PlexActors.Values)
        {
            var dbActor = actorsDb.FirstOrDefault(x => x.Id == returnedActor.Id);
            dbActor.ShouldNotBeNull($"Actor with ID {returnedActor.Id} should exist in database");
            dbActor.Name.ShouldBe(returnedActor.Name);
            dbActor.Key.ShouldBe(returnedActor.Key);
        }

        foreach (var returnedGenre in result.Value.PlexGenres.Values)
        {
            var dbGenre = genresDb.FirstOrDefault(x => x.Id == returnedGenre.Id);
            dbGenre.ShouldNotBeNull($"Genre with ID {returnedGenre.Id} should exist in database");
            dbGenre.Name.ShouldBe(returnedGenre.Name);
            dbGenre.Key.ShouldBe(returnedGenre.Key);
        }

        foreach (var returnedCountry in result.Value.PlexCountries.Values)
        {
            var dbCountry = countriesDb.FirstOrDefault(x => x.Id == returnedCountry.Id);
            dbCountry.ShouldNotBeNull($"Country with ID {returnedCountry.Id} should exist in database");
            dbCountry.Name.ShouldBe(returnedCountry.Name);
            dbCountry.Key.ShouldBe(returnedCountry.Key);
        }

        // Verify source data mapping is correct
        foreach (var sourceActor in actors.Where(x => !string.IsNullOrEmpty(x.Key)))
        {
            result.Value.PlexActors.ShouldContainKey(
                sourceActor.Key,
                $"Response should contain actor with PlexId {sourceActor.Key}"
            );
            var responseActor = result.Value.PlexActors[sourceActor.Key];
            responseActor.Name.ShouldBe(sourceActor.Name);
        }

        foreach (var sourceGenre in genres.Where(x => !string.IsNullOrEmpty(x.Key)))
        {
            result.Value.PlexGenres.ShouldContainKey(
                sourceGenre.Key,
                $"Response should contain genre with PlexId {sourceGenre.Key}"
            );
            var responseGenre = result.Value.PlexGenres[sourceGenre.Key];
            responseGenre.Name.ShouldBe(sourceGenre.Name);
        }

        foreach (var sourceCountry in countries.Where(x => !string.IsNullOrEmpty(x.Key)))
        {
            result.Value.PlexCountries.ShouldContainKey(
                sourceCountry.Key,
                $"Response should contain country with PlexId {sourceCountry.Key}"
            );
            var responseCountry = result.Value.PlexCountries[sourceCountry.Key];
            responseCountry.Name.ShouldBe(sourceCountry.Name);
        }
    }
}
