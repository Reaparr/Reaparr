namespace Reaparr.Application.UnitTests;

public class GetMetadataFilterUnitTests
    : BaseEndpointUnitTest<GetMetadataFilter, GetMetadataFilterRequest, ResultDTO<PlexMediaFilterMetadataDTO>>
{
    [Test]
    [Arguments(PlexMediaType.Movie, false)]
    [Arguments(PlexMediaType.Movie, true)]
    [Arguments(PlexMediaType.OtherVideos, false)]
    [Arguments(PlexMediaType.OtherVideos, true)]
    public async Task ShouldReturnOnlyScopedDistinctQualities_WhenBrowsingMetadata(
        PlexMediaType mediaType,
        bool allLibraries
    )
    {
        // Arrange
        await SetupDatabase(
            8238,
            cfg =>
            {
                cfg.PlexMovieLibraryCount = 2;
                cfg.PlexOtherVideoLibraryCount = 2;
                cfg.MovieCount = 2;
                cfg.OtherVideoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var libraryIds = await dbContext
            .PlexLibraries.Where(x => x.Type == mediaType)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var targetId = libraryIds[0];
        await dbContext.PlexMovieData.ExecuteUpdateAsync(
            x =>
                x.SetProperty(
                    y => y.VideoQuality,
                    y =>
                        mediaType == PlexMediaType.Movie
                            ? (y.PlexLibraryId == targetId ? VideoQuality.FullHD : VideoQuality.UHD_4K)
                            : VideoQuality.SD
                ),
            CancellationToken
        );
        await dbContext.PlexOtherVideoData.ExecuteUpdateAsync(
            x =>
                x.SetProperty(
                    y => y.VideoQuality,
                    y =>
                        mediaType == PlexMediaType.OtherVideos
                            ? (y.PlexLibraryId == targetId ? VideoQuality.FullHD : VideoQuality.UHD_4K)
                            : VideoQuality.SD
                ),
            CancellationToken
        );
        var request = new GetMetadataFilterRequest
        {
            PlexLibraryId = allLibraries ? 0 : targetId,
            MediaType = mediaType,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        var response = endpointResult.Response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response
            .Value.ShouldNotBeNull()
            .Qualities.Order()
            .ShouldBe(
                allLibraries
                    ? new[] { VideoQuality.FullHD.ToId(), VideoQuality.UHD_4K.ToId() }.Order()
                    : new[] { VideoQuality.FullHD.ToId() }
            );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldKeepAudioAndVideoGenreIdsScoped_WhenNamesAndIdsOverlap(bool omitMediaType)
    {
        // Arrange
        await SetupDatabase(
            8239,
            cfg =>
            {
                cfg.PlexMovieLibraryCount = 1;
                cfg.PlexMusicLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var videoLibrary = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.Movie,
            CancellationToken
        );
        var audioLibrary = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.MusicArtist,
            CancellationToken
        );
        var videoGenre = new PlexGenreVideo
        {
            Name = "Shared",
            Key = "shared",
            Type = PlexGenreType.Drama,
        };
        var audioGenre = new PlexGenreAudio { Name = "Shared", Key = "shared" };
        dbContext.PlexGenresVideo.Add(videoGenre);
        dbContext.PlexGenresAudio.Add(audioGenre);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.PlexLibraryGenresVideo.Add(new PlexLibraryGenresVideo(videoLibrary.Id, videoGenre.Id));
        dbContext.PlexLibraryGenresAudio.Add(new PlexLibraryGenresAudio(audioLibrary.Id, audioGenre.Id));
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var videoResult = await TestEndpointHandleAsync(
            new GetMetadataFilterRequest
            {
                PlexLibraryId = videoLibrary.Id,
                MediaType = omitMediaType ? PlexMediaType.None : PlexMediaType.Movie,
            }
        );
        var audioResult = await TestEndpointHandleAsync(
            new GetMetadataFilterRequest
            {
                PlexLibraryId = audioLibrary.Id,
                MediaType = omitMediaType ? PlexMediaType.None : PlexMediaType.MusicArtist,
            }
        );

        // Assert
        videoResult.IsValid.ShouldBeTrue();
        videoResult.StatusCode.ShouldBe(200);
        var videoResponse = videoResult.Response.ShouldNotBeNull();
        videoResponse.IsSuccess.ShouldBeTrue();
        videoResponse.Errors.Count.ShouldBe(0);
        videoResponse.Value.ShouldNotBeNull().Genres.ShouldBe([videoGenre.Id]);
        audioResult.IsValid.ShouldBeTrue();
        audioResult.StatusCode.ShouldBe(200);
        var audioResponse = audioResult.Response.ShouldNotBeNull();
        audioResponse.IsSuccess.ShouldBeTrue();
        audioResponse.Errors.Count.ShouldBe(0);
        audioResponse.Value.ShouldNotBeNull().Genres.ShouldBe([audioGenre.Id]);
        videoGenre.Id.ShouldBe(audioGenre.Id);
    }

    [Test]
    [Arguments(PlexMediaType.Movie, 8250)]
    [Arguments(PlexMediaType.TvShow, 8251)]
    [Arguments(PlexMediaType.OtherVideos, 8252)]
    public async Task ShouldReturnFilterIdsOnlyFromAllowedLibraries_WhenAllLibrariesAreRequested(
        PlexMediaType mediaType,
        int seed
    )
    {
        // Arrange
        await SetupDatabase(seed, config => MetadataScopeTestFixture.ConfigureVideoSeed(config, mediaType, 4));
        var dbContext = IDbContext;
        var fixture = new MetadataScopeTestFixture(dbContext, CancellationToken);
        var libraries = await dbContext
            .PlexLibraries.IgnoreQueryFilters()
            .Where(x => x.Type == mediaType)
            .OrderBy(x => x.PlexServerId)
            .ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(4);
        var rows = await fixture.AddVideoMetadataAsync(libraries, "filter-scope");
        await fixture.SetVideoQualitiesAsync(
            mediaType,
            libraries,
            [VideoQuality.FullHD, VideoQuality.SD, VideoQuality.UHD_4K, VideoQuality.QHD]
        );
        var account = await dbContext.PlexAccounts.SingleAsync(CancellationToken);
        await fixture.RevokeAndDisableLibrariesAsync(account, libraries[1], libraries[2], libraries[3]);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetMetadataFilterRequest { PlexLibraryId = 0, MediaType = mediaType }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        var response = endpointResult.Response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var filterMetadata = response.Value.ShouldNotBeNull();
        filterMetadata.Roles.ShouldBe([rows.Actors[0].Id]);
        filterMetadata.Countries.ShouldBe([rows.Countries[0].Id]);
        filterMetadata.Genres.ShouldBe([rows.Genres[0].Id]);
        filterMetadata.Qualities.ShouldBe([VideoQuality.FullHD.ToId()]);
    }

    [Test]
    public async Task ShouldUseAudioGenresFromAllowedLibraries_WhenAllLibrariesAreRequested()
    {
        // Arrange
        await SetupDatabase(
            8253,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
            }
        );
        var dbContext = IDbContext;
        var fixture = new MetadataScopeTestFixture(dbContext, CancellationToken);
        var libraries = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.MusicArtist)
            .OrderBy(x => x.PlexServerId)
            .ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        var audioGenres = await fixture.AddAudioGenresAsync(libraries, "filter-audio");
        var account = await dbContext.PlexAccounts.SingleAsync(CancellationToken);
        await fixture.RevokeLibraryAccessAsync(account, libraries[1]);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetMetadataFilterRequest { PlexLibraryId = 0, MediaType = PlexMediaType.MusicArtist }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        var response = endpointResult.Response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var filterMetadata = response.Value.ShouldNotBeNull();
        filterMetadata.Roles.ShouldBeEmpty();
        filterMetadata.Countries.ShouldBeEmpty();
        filterMetadata.Genres.ShouldBe([audioGenres[0].Id]);
        filterMetadata.Qualities.ShouldBeEmpty();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ShouldApplyOwnedAndOfflineFlagsToAllLibraryFilterMetadata(
        bool filterOwnedMedia,
        bool filterOfflineMedia
    )
    {
        // Arrange
        await SetupDatabase(
            8254,
            config => MetadataScopeTestFixture.ConfigureVideoSeed(config, PlexMediaType.Movie, 3)
        );
        var dbContext = IDbContext;
        var fixture = new MetadataScopeTestFixture(dbContext, CancellationToken);
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.PlexServerId).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(3);
        var rows = await fixture.AddVideoMetadataAsync(libraries, "filter-flag");
        await fixture.SetVideoQualitiesAsync(
            PlexMediaType.Movie,
            libraries,
            [VideoQuality.FullHD, VideoQuality.FullHD, VideoQuality.FullHD]
        );
        var account = await dbContext.PlexAccounts.SingleAsync(CancellationToken);
        var accountServers = await dbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == account.Id)
            .ToListAsync(CancellationToken);
        var accountLibraries = await dbContext
            .PlexAccountLibraries.Where(x => x.PlexAccountId == account.Id)
            .ToListAsync(CancellationToken);
        accountServers.Count.ShouldBe(3);
        accountLibraries.Count.ShouldBe(3);
        await fixture.ConfigureOwnedOfflineLibrariesAsync(account, libraries);
        var expectedIndexes = (filterOwnedMedia, filterOfflineMedia) switch
        {
            (false, false) => new[] { 0, 1, 2 },
            (true, false) => new[] { 1, 2 },
            (false, true) => new[] { 0, 1 },
            (true, true) => new[] { 1 },
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetMetadataFilterRequest
            {
                PlexLibraryId = 0,
                MediaType = PlexMediaType.Movie,
                FilterOwnedMedia = filterOwnedMedia,
                FilterOfflineMedia = filterOfflineMedia,
            }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        var response = endpointResult.Response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var filterMetadata = response.Value.ShouldNotBeNull();
        filterMetadata.Roles.Order().ShouldBe(expectedIndexes.Select(index => rows.Actors[index].Id).Order());
        filterMetadata.Countries.Order().ShouldBe(expectedIndexes.Select(index => rows.Countries[index].Id).Order());
        filterMetadata.Genres.Order().ShouldBe(expectedIndexes.Select(index => rows.Genres[index].Id).Order());
        filterMetadata.Qualities.ShouldBe([VideoQuality.FullHD.ToId()]);
    }

    [Test]
    public async Task ShouldKeepDirectLibraryFilterMetadata_WhenScopeFlagsAreSet()
    {
        // Arrange
        await SetupDatabase(
            8255,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.PlexServerId).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        var targetLibrary = libraries[0];
        await dbContext.PlexMovieData.ExecuteUpdateAsync(
            x =>
                x.SetProperty(
                    y => y.VideoQuality,
                    y => y.PlexLibraryId == targetLibrary.Id ? VideoQuality.FullHD : VideoQuality.UHD_4K
                ),
            CancellationToken
        );
        var accountId = await dbContext.PlexAccounts.Select(x => x.Id).SingleAsync(CancellationToken);
        var targetAccountServer = await dbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == accountId && x.PlexServerId == targetLibrary.PlexServerId)
            .AsTracking()
            .SingleAsync(CancellationToken);
        targetAccountServer.IsServerOwned = true;
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetMetadataFilterRequest
            {
                PlexLibraryId = targetLibrary.Id,
                MediaType = PlexMediaType.Movie,
                FilterOwnedMedia = true,
                FilterOfflineMedia = true,
            }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        var response = endpointResult.Response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response.Value.ShouldNotBeNull().Qualities.ShouldBe([VideoQuality.FullHD.ToId()]);
    }
}
