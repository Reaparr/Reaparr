namespace Reaparr.Application.UnitTests;

public class SyncPlexLibraryMediaMetaDataCommandUnitTests : BaseCommandUnitTest<SyncPlexLibraryMediaMetaDataCommand>
{
    [Test]
    public async Task ShouldReturnNotFound_WhenLibraryDoesNotExist()
    {
        // Arrange
        await SetupDatabase(1223, config => config.PlexMovieLibraryCount = 1);
        var dbContext = IDbContext;
        var plexLibrary = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var missingLibraryId = plexLibrary.Id + 1;
        (await dbContext.PlexLibraries.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe([plexLibrary.Id]);
        plexLibrary.Id = missingLibraryId;

        // Act
        var result = await TestHandlerExecuteAsync(
            new SyncPlexLibraryMediaMetaDataCommand(new InsertMediaMetaDataCommandResponse(plexLibrary))
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Has404NotFoundError().ShouldBeTrue();
        (await dbContext.PlexLibraries.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe([
            missingLibraryId - 1,
        ]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReplaceOnlyTargetLibraryMetadata_WithAuthoritativePlexSnapshot(bool emptySnapshot)
    {
        // Arrange
        await SetupDatabase(713480, config => config.PlexMovieLibraryCount = 2);
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var target = libraries[0];
        var control = libraries[1];
        var actors = Enumerable
            .Range(1, 3)
            .Select(x => new PlexActor { Key = $"actor-{x}", Name = $"Actor {x}" })
            .ToArray();
        var videoGenres = Enumerable
            .Range(1, 3)
            .Select(x => new PlexGenreVideo { Key = $"video-{x}", Name = $"Video genre {x}" })
            .ToArray();
        var audioGenres = Enumerable
            .Range(1, 3)
            .Select(x => new PlexGenreAudio { Key = $"audio-{x}", Name = $"Audio genre {x}" })
            .ToArray();
        var countries = Enumerable
            .Range(1, 3)
            .Select(x => new PlexCountry { Key = $"country-{x}", Name = $"Country {x}" })
            .ToArray();
        dbContext.PlexActors.AddRange(actors);
        dbContext.PlexGenresVideo.AddRange(videoGenres);
        dbContext.PlexGenresAudio.AddRange(audioGenres);
        dbContext.PlexCountries.AddRange(countries);
        await dbContext.SaveChangesAsync(CancellationToken);
        foreach (var library in libraries)
        {
            dbContext.PlexLibraryActors.Add(new PlexLibraryActors(library.Id, actors[0].Id));
            dbContext.PlexLibraryGenresVideo.Add(new PlexLibraryGenresVideo(library.Id, videoGenres[0].Id));
            dbContext.PlexLibraryGenresAudio.Add(new PlexLibraryGenresAudio(library.Id, audioGenres[0].Id));
            dbContext.PlexLibraryCountries.Add(new PlexLibraryCountries(library.Id, countries[0].Id));
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            x =>
                x.SetProperty(p => p.ActorsCount, 1)
                    .SetProperty(p => p.GenresCount, 2)
                    .SetProperty(p => p.CountriesCount, 1),
            CancellationToken
        );
        var beforeActors = await dbContext
            .PlexLibraryActors.OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, MetadataId = x.PlexActorId })
            .ToListAsync(CancellationToken);
        var beforeVideoGenres = await dbContext
            .PlexLibraryGenresVideo.OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, MetadataId = x.PlexGenreId })
            .ToListAsync(CancellationToken);
        var beforeAudioGenres = await dbContext
            .PlexLibraryGenresAudio.OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, MetadataId = x.PlexGenreId })
            .ToListAsync(CancellationToken);
        var beforeCountries = await dbContext
            .PlexLibraryCountries.OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, MetadataId = x.PlexCountryId })
            .ToListAsync(CancellationToken);
        beforeActors.ShouldBe(libraries.Select(x => new { PlexLibraryId = x.Id, MetadataId = actors[0].Id }));
        beforeVideoGenres.ShouldBe(libraries.Select(x => new { PlexLibraryId = x.Id, MetadataId = videoGenres[0].Id }));
        beforeAudioGenres.ShouldBe(libraries.Select(x => new { PlexLibraryId = x.Id, MetadataId = audioGenres[0].Id }));
        beforeCountries.ShouldBe(libraries.Select(x => new { PlexLibraryId = x.Id, MetadataId = countries[0].Id }));
        var metadata = new InsertMediaMetaDataCommandResponse(target)
        {
            PlexActors = actors.Skip(emptySnapshot ? actors.Length : 1).ToDictionary(x => x.Key),
            PlexGenresVideo = videoGenres.Skip(emptySnapshot ? videoGenres.Length : 1).ToDictionary(x => x.Key),
            PlexGenresAudio = audioGenres.Skip(emptySnapshot ? audioGenres.Length : 1).ToDictionary(x => x.Key),
            PlexCountries = countries.Skip(emptySnapshot ? countries.Length : 1).ToDictionary(x => x.Key),
        };

        // Act
        var result = await TestHandlerExecuteAsync(new SyncPlexLibraryMediaMetaDataCommand(metadata));

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        (
            await dbContext
                .PlexLibraryActors.OrderBy(x => x.PlexLibraryId)
                .ThenBy(x => x.PlexActorId)
                .Select(x => new { x.PlexLibraryId, MetadataId = x.PlexActorId })
                .ToListAsync(CancellationToken)
        ).ShouldBe(
            metadata
                .PlexActors.Values.Select(x => new { PlexLibraryId = target.Id, MetadataId = x.Id })
                .Concat(beforeActors.Where(x => x.PlexLibraryId == control.Id))
        );
        (
            await dbContext
                .PlexLibraryGenresVideo.OrderBy(x => x.PlexLibraryId)
                .ThenBy(x => x.PlexGenreId)
                .Select(x => new { x.PlexLibraryId, MetadataId = x.PlexGenreId })
                .ToListAsync(CancellationToken)
        ).ShouldBe(
            metadata
                .PlexGenresVideo.Values.Select(x => new { PlexLibraryId = target.Id, MetadataId = x.Id })
                .Concat(beforeVideoGenres.Where(x => x.PlexLibraryId == control.Id))
        );
        (
            await dbContext
                .PlexLibraryGenresAudio.OrderBy(x => x.PlexLibraryId)
                .ThenBy(x => x.PlexGenreId)
                .Select(x => new { x.PlexLibraryId, MetadataId = x.PlexGenreId })
                .ToListAsync(CancellationToken)
        ).ShouldBe(
            metadata
                .PlexGenresAudio.Values.Select(x => new { PlexLibraryId = target.Id, MetadataId = x.Id })
                .Concat(beforeAudioGenres.Where(x => x.PlexLibraryId == control.Id))
        );
        (
            await dbContext
                .PlexLibraryCountries.OrderBy(x => x.PlexLibraryId)
                .ThenBy(x => x.PlexCountryId)
                .Select(x => new { x.PlexLibraryId, MetadataId = x.PlexCountryId })
                .ToListAsync(CancellationToken)
        ).ShouldBe(
            metadata
                .PlexCountries.Values.Select(x => new { PlexLibraryId = target.Id, MetadataId = x.Id })
                .Concat(beforeCountries.Where(x => x.PlexLibraryId == control.Id))
        );
        (
            await dbContext
                .PlexLibraries.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.ActorsCount,
                    x.GenresCount,
                    x.CountriesCount,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe([
            new
            {
                Id = target.Id,
                ActorsCount = emptySnapshot ? 0 : 2,
                GenresCount = emptySnapshot ? 0 : 4,
                CountriesCount = emptySnapshot ? 0 : 2,
            },
            new
            {
                Id = control.Id,
                ActorsCount = 1,
                GenresCount = 2,
                CountriesCount = 1,
            },
        ]);
        (await dbContext.PlexActors.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            actors.Select(x => x.Id)
        );
        (await dbContext.PlexGenresVideo.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            videoGenres.Select(x => x.Id)
        );
        (await dbContext.PlexGenresAudio.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            audioGenres.Select(x => x.Id)
        );
        (await dbContext.PlexCountries.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            countries.Select(x => x.Id)
        );
    }

    [Test]
    public async Task ShouldNotWriteMetadataOrCounters_WhenIncomingSetsAreIdentical()
    {
        // Arrange
        await SetupDatabase(713481, config => config.PlexMovieLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var actors = new[]
        {
            new PlexActor { Key = "first", Name = "First actor" },
            new PlexActor { Key = "second", Name = "Second actor" },
        };
        var videoGenre = new PlexGenreVideo { Key = "video", Name = "Video genre" };
        var audioGenre = new PlexGenreAudio { Key = "audio", Name = "Audio genre" };
        var country = new PlexCountry { Key = "country", Name = "Country" };
        dbContext.PlexActors.AddRange(actors);
        dbContext.PlexGenresVideo.Add(videoGenre);
        dbContext.PlexGenresAudio.Add(audioGenre);
        dbContext.PlexCountries.Add(country);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.PlexLibraryActors.AddRange(actors.Select(x => new PlexLibraryActors(library.Id, x.Id)));
        dbContext.PlexLibraryGenresVideo.Add(new PlexLibraryGenresVideo(library.Id, videoGenre.Id));
        dbContext.PlexLibraryGenresAudio.Add(new PlexLibraryGenresAudio(library.Id, audioGenre.Id));
        dbContext.PlexLibraryCountries.Add(new PlexLibraryCountries(library.Id, country.Id));
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            x =>
                x.SetProperty(p => p.ActorsCount, 2)
                    .SetProperty(p => p.GenresCount, 2)
                    .SetProperty(p => p.CountriesCount, 1),
            CancellationToken
        );
        (
            await dbContext
                .PlexLibraryActors.OrderBy(x => x.PlexActorId)
                .Select(x => x.PlexActorId)
                .ToListAsync(CancellationToken)
        ).ShouldBe(actors.Select(x => x.Id));
        (await dbContext.PlexLibraryGenresVideo.Select(x => x.PlexGenreId).ToListAsync(CancellationToken)).ShouldBe([
            videoGenre.Id,
        ]);
        (await dbContext.PlexLibraryGenresAudio.Select(x => x.PlexGenreId).ToListAsync(CancellationToken)).ShouldBe([
            audioGenre.Id,
        ]);
        (await dbContext.PlexLibraryCountries.Select(x => x.PlexCountryId).ToListAsync(CancellationToken)).ShouldBe([
            country.Id,
        ]);
        // Reject real writes so deleting and reinserting the same associations cannot pass as a no-op.
        await ((DbContext)dbContext).Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER reject_actor_insert BEFORE INSERT ON PlexLibraryActors
            BEGIN SELECT RAISE(ABORT, 'Identical actors must not be inserted'); END;
            CREATE TRIGGER reject_actor_delete BEFORE DELETE ON PlexLibraryActors
            BEGIN SELECT RAISE(ABORT, 'Identical actors must not be deleted'); END;
            CREATE TRIGGER reject_video_genre_insert BEFORE INSERT ON PlexLibraryGenresVideo
            BEGIN SELECT RAISE(ABORT, 'Identical video genres must not be inserted'); END;
            CREATE TRIGGER reject_video_genre_delete BEFORE DELETE ON PlexLibraryGenresVideo
            BEGIN SELECT RAISE(ABORT, 'Identical video genres must not be deleted'); END;
            CREATE TRIGGER reject_audio_genre_insert BEFORE INSERT ON PlexLibraryGenresAudio
            BEGIN SELECT RAISE(ABORT, 'Identical audio genres must not be inserted'); END;
            CREATE TRIGGER reject_audio_genre_delete BEFORE DELETE ON PlexLibraryGenresAudio
            BEGIN SELECT RAISE(ABORT, 'Identical audio genres must not be deleted'); END;
            CREATE TRIGGER reject_country_insert BEFORE INSERT ON PlexLibraryCountries
            BEGIN SELECT RAISE(ABORT, 'Identical countries must not be inserted'); END;
            CREATE TRIGGER reject_country_delete BEFORE DELETE ON PlexLibraryCountries
            BEGIN SELECT RAISE(ABORT, 'Identical countries must not be deleted'); END;
            CREATE TRIGGER reject_counter_update BEFORE UPDATE ON PlexLibraries
            BEGIN SELECT RAISE(ABORT, 'Identical counters must not be rewritten'); END;
            """,
            CancellationToken
        );
        var metadata = new InsertMediaMetaDataCommandResponse(library)
        {
            PlexActors = actors.Reverse().ToDictionary(x => x.Key),
            PlexGenresVideo = new() { [videoGenre.Key] = videoGenre },
            PlexGenresAudio = new() { [audioGenre.Key] = audioGenre },
            PlexCountries = new() { [country.Key] = country },
        };

        // Act
        var result = await TestHandlerExecuteAsync(new SyncPlexLibraryMediaMetaDataCommand(metadata));

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        (
            await dbContext
                .PlexLibraryActors.OrderBy(x => x.PlexActorId)
                .Select(x => x.PlexActorId)
                .ToListAsync(CancellationToken)
        ).ShouldBe(actors.Select(x => x.Id));
        (await dbContext.PlexLibraryGenresVideo.Select(x => x.PlexGenreId).ToListAsync(CancellationToken)).ShouldBe([
            videoGenre.Id,
        ]);
        (await dbContext.PlexLibraryGenresAudio.Select(x => x.PlexGenreId).ToListAsync(CancellationToken)).ShouldBe([
            audioGenre.Id,
        ]);
        (await dbContext.PlexLibraryCountries.Select(x => x.PlexCountryId).ToListAsync(CancellationToken)).ShouldBe([
            country.Id,
        ]);
        var counters = await dbContext
            .PlexLibraries.Select(x => new
            {
                x.ActorsCount,
                x.GenresCount,
                x.CountriesCount,
            })
            .SingleAsync(CancellationToken);
        counters.ShouldBe(
            new
            {
                ActorsCount = 2,
                GenresCount = 2,
                CountriesCount = 1,
            }
        );
    }

    [Test]
    [Arguments(nameof(PlexActor), false)]
    [Arguments(nameof(PlexActor), true)]
    [Arguments(nameof(PlexGenreVideo), false)]
    [Arguments(nameof(PlexGenreVideo), true)]
    [Arguments(nameof(PlexGenreAudio), false)]
    [Arguments(nameof(PlexGenreAudio), true)]
    [Arguments(nameof(PlexCountry), false)]
    [Arguments(nameof(PlexCountry), true)]
    public async Task ShouldPreserveCounters_WhenMetadataTransactionFailsOrIsCancelled(
        string metadataType,
        bool cancelled
    )
    {
        // Arrange
        await SetupDatabase(713482, config => config.PlexMovieLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var actor = new PlexActor { Key = "actor", Name = "Incoming actor" };
        var videoGenre = new PlexGenreVideo { Key = "video", Name = "Incoming video genre" };
        var audioGenre = new PlexGenreAudio { Key = "audio", Name = "Incoming audio genre" };
        var country = new PlexCountry { Key = "country", Name = "Incoming country" };
        dbContext.PlexActors.Add(actor);
        dbContext.PlexGenresVideo.Add(videoGenre);
        dbContext.PlexGenresAudio.Add(audioGenre);
        dbContext.PlexCountries.Add(country);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            x =>
                x.SetProperty(p => p.ActorsCount, 17)
                    .SetProperty(p => p.GenresCount, 23)
                    .SetProperty(p => p.CountriesCount, 31),
            CancellationToken
        );
        var beforeCounters = await dbContext
            .PlexLibraries.Select(x => new
            {
                x.Id,
                x.ActorsCount,
                x.GenresCount,
                x.CountriesCount,
            })
            .SingleAsync(CancellationToken);
        beforeCounters.ShouldBe(
            new
            {
                library.Id,
                ActorsCount = 17,
                GenresCount = 23,
                CountriesCount = 31,
            }
        );
        (await dbContext.PlexLibraryActors.Select(x => x.PlexActorId).ToListAsync(CancellationToken)).ShouldBe(
            Array.Empty<int>()
        );
        (await dbContext.PlexLibraryGenresAudio.Select(x => x.PlexGenreId).ToListAsync(CancellationToken)).ShouldBe(
            Array.Empty<int>()
        );
        (await dbContext.PlexLibraryCountries.Select(x => x.PlexCountryId).ToListAsync(CancellationToken)).ShouldBe(
            Array.Empty<int>()
        );
        (await dbContext.PlexLibraryGenresVideo.Select(x => x.PlexGenreId).ToListAsync(CancellationToken)).ShouldBe(
            Array.Empty<int>()
        );
        var transactionResult = cancelled
            ? ResultExtensions.TaskIsCancelled("metadata transaction")
            : Result.Fail("Metadata transaction failed");
        var metadata = new InsertMediaMetaDataCommandResponse(library)
        {
            PlexActors = metadataType == nameof(PlexActor) ? new() { [actor.Key] = actor } : new(),
            PlexGenresVideo = metadataType == nameof(PlexGenreVideo) ? new() { [videoGenre.Key] = videoGenre } : new(),
            PlexGenresAudio = metadataType == nameof(PlexGenreAudio) ? new() { [audioGenre.Key] = audioGenre } : new(),
            PlexCountries = metadataType == nameof(PlexCountry) ? new() { [country.Key] = country } : new(),
        };
        var mockContext = new Mock<IReaparrDbContext>(MockBehavior.Strict);
        mockContext.SetupGet(x => x.PlexLibraries).Returns(dbContext.PlexLibraries);
        mockContext.SetupGet(x => x.PlexLibraryActors).Returns(dbContext.PlexLibraryActors);
        mockContext.SetupGet(x => x.PlexLibraryGenresVideo).Returns(dbContext.PlexLibraryGenresVideo);
        mockContext.SetupGet(x => x.PlexLibraryGenresAudio).Returns(dbContext.PlexLibraryGenresAudio);
        mockContext.SetupGet(x => x.PlexLibraryCountries).Returns(dbContext.PlexLibraryCountries);
        mockContext
            .Setup(x =>
                x.ExecuteTransactionAsync(
                    It.IsAny<Func<IReaparrDbContext, CancellationToken, Task>>(),
                    It.Is<CancellationToken>(ct => ct == CancellationToken)
                )
            )
            .ReturnsAsync(transactionResult)
            .Verifiable(Times.Once());
        SetupDependencies(builder =>
            builder.RegisterInstance(mockContext.Object).As<IReaparrDbContext>().ExternallyOwned()
        );

        // Act
        var result = await TestHandlerExecuteAsync(new SyncPlexLibraryMediaMetaDataCommand(metadata));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(cancelled);
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeSameAs(transactionResult.Errors.Single());
        (
            await dbContext
                .PlexLibraries.Select(x => new
                {
                    x.Id,
                    x.ActorsCount,
                    x.GenresCount,
                    x.CountriesCount,
                })
                .SingleAsync(CancellationToken)
        ).ShouldBe(beforeCounters);
        (await dbContext.PlexLibraryGenresVideo.Select(x => x.PlexGenreId).ToListAsync(CancellationToken)).ShouldBe(
            Array.Empty<int>()
        );
        (await dbContext.PlexLibraryActors.Select(x => x.PlexActorId).ToListAsync(CancellationToken)).ShouldBe(
            Array.Empty<int>()
        );
        (await dbContext.PlexLibraryGenresAudio.Select(x => x.PlexGenreId).ToListAsync(CancellationToken)).ShouldBe(
            Array.Empty<int>()
        );
        (await dbContext.PlexLibraryCountries.Select(x => x.PlexCountryId).ToListAsync(CancellationToken)).ShouldBe(
            Array.Empty<int>()
        );
        mockContext.Verify();
    }
}
