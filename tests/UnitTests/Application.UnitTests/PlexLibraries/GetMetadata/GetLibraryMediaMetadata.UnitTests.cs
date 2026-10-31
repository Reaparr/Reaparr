namespace Reaparr.Application.UnitTests;

public class GetLibraryMediaMetadataUnitTests
    : BaseEndpointUnitTest<GetLibraryMediaMetadata, GetLibraryMediaMetadataRequest, PlexMediaMetadataDTO>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnExactOtherVideoQualityCounts_WhenBrowsingMetadata(bool allLibraries)
    {
        // Arrange
        await SetupDatabase(
            8239,
            cfg =>
            {
                cfg.PlexOtherVideoLibraryCount = 2;
                cfg.OtherVideoCount = 2;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraryIds = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.OtherVideos)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var targetId = libraryIds[0];
        var originals = await dbContext
            .PlexOtherVideoData.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexLibraryId })
            .ToListAsync(CancellationToken);
        originals
            .Select(x => x.PlexLibraryId)
            .Order()
            .ShouldBe(new[] { libraryIds[0], libraryIds[0], libraryIds[1], libraryIds[1] });
        await dbContext.PlexOtherVideoData.ExecuteUpdateAsync(
            x =>
                x.SetProperty(
                    y => y.VideoQuality,
                    y => y.PlexLibraryId == targetId ? VideoQuality.FullHD : VideoQuality.UHD_4K
                ),
            CancellationToken
        );
        await dbContext.PlexMovieData.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.VideoQuality, VideoQuality.SD),
            CancellationToken
        );
        var request = new GetLibraryMediaMetadataRequest
        {
            PlexLibraryId = allLibraries ? 0 : targetId,
            MediaType = PlexMediaType.OtherVideos,
            FilterOwnedMedia = true,
            FilterOfflineMedia = true,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var response = await ReadMetadataResponseAsync(endpointResult.Endpoint.HttpContext.Response.Body);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var metadata = response.Value.ShouldNotBeNull();
        metadata.MediaCount.ShouldBe(allLibraries ? 4 : 2);
        metadata.QualityCount.ShouldBe(allLibraries ? 2 : 1);
        metadata
            .Qualities.OrderBy(x => x.Quality)
            .Select(x => (x.Quality, x.Name, x.Count))
            .ShouldBe(
                allLibraries
                    ? new[]
                    {
                        (VideoQuality.FullHD, ((int)VideoQuality.FullHD).ToString(), 2),
                        (VideoQuality.UHD_4K, ((int)VideoQuality.UHD_4K).ToString(), 2),
                    }
                    : new[] { (VideoQuality.FullHD, ((int)VideoQuality.FullHD).ToString(), 2) }
            );
    }

    [Test]
    [Arguments(PlexMediaType.Movie, 8240)]
    [Arguments(PlexMediaType.TvShow, 8241)]
    [Arguments(PlexMediaType.OtherVideos, 8242)]
    public async Task ShouldReturnMetadataOnlyFromAllowedLibraries_WhenAllLibrariesAreRequested(
        PlexMediaType mediaType,
        int seed
    )
    {
        // Arrange
        await SetupDatabase(seed, config => MetadataScopeTestFixture.ConfigureVideoSeed(config, mediaType, 4));
        var dbContext = IDbContext;
        var fixture = new MetadataScopeTestFixture(dbContext, CancellationToken);
        var servers = await dbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var libraries = await dbContext
            .PlexLibraries.IgnoreQueryFilters()
            .Where(x => x.Type == mediaType)
            .OrderBy(x => x.PlexServerId)
            .ToListAsync(CancellationToken);
        servers.Count.ShouldBe(4);
        libraries.Count.ShouldBe(4);
        var rows = await fixture.AddVideoMetadataAsync(libraries, "scope");
        await fixture.SetVideoQualitiesAsync(
            mediaType,
            libraries,
            [VideoQuality.FullHD, VideoQuality.SD, VideoQuality.UHD_4K, VideoQuality.QHD]
        );
        var account = await dbContext.PlexAccounts.SingleAsync(CancellationToken);
        var accountServers = await dbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == account.Id)
            .Select(x => x.PlexServerId)
            .OrderBy(x => x)
            .ToListAsync(CancellationToken);
        accountServers.ShouldBe(servers.Select(x => x.Id));
        await fixture.RevokeAndDisableLibrariesAsync(account, libraries[1], libraries[2], libraries[3]);

        (
            await dbContext
                .PlexAccountServers.Select(x => x.PlexServerId)
                .OrderBy(x => x)
                .ToListAsync(CancellationToken)
        ).ShouldBe(servers.Where(x => x.Id != libraries[1].PlexServerId).Select(x => x.Id));
        (
            await dbContext
                .PlexAccountLibraries.Select(x => x.PlexLibraryId)
                .OrderBy(x => x)
                .ToListAsync(CancellationToken)
        ).ShouldBe(libraries.Where(x => x.Id != libraries[2].Id).Select(x => x.Id));
        (
            await dbContext
                .PlexLibraries.IgnoreQueryFilters()
                .Where(x => x.Id == libraries[3].Id)
                .Select(x => x.IsEnabled)
                .SingleAsync(CancellationToken)
        ).ShouldBeFalse();

        // Act
        var request = new GetLibraryMediaMetadataRequest { PlexLibraryId = 0, MediaType = mediaType };
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        var response = (
            await ReadMetadataResponseAsync(endpointResult.Endpoint.HttpContext.Response.Body)
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var metadata = response.Value.ShouldNotBeNull();
        metadata.MediaCount.ShouldBe(1);
        metadata.RoleCount.ShouldBe(1);
        metadata.CountryCount.ShouldBe(1);
        metadata.GenreCount.ShouldBe(1);
        metadata.QualityCount.ShouldBe(1);
        metadata
            .Roles.OrderBy(x => x.Id)
            .Select(x => (x.Id, x.Name))
            .ShouldBe([(rows.Actors[0].Id, rows.Actors[0].Name)]);
        metadata
            .Countries.OrderBy(x => x.Id)
            .Select(x => (x.Id, x.Name))
            .ShouldBe([(rows.Countries[0].Id, rows.Countries[0].Name)]);
        metadata
            .Genres.OrderBy(x => x.Id)
            .Select(x => (x.Id, x.Name))
            .ShouldBe([(rows.Genres[0].Id, rows.Genres[0].Name)]);
        metadata
            .Qualities.OrderBy(x => x.Quality)
            .Select(x => (x.Quality, x.Name, x.Count))
            .ShouldBe([(VideoQuality.FullHD, ((int)VideoQuality.FullHD).ToString(), 1)]);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ShouldApplyOwnedAndOfflineFlagsToAllLibraryMetadata(
        bool filterOwnedMedia,
        bool filterOfflineMedia
    )
    {
        // Arrange
        await SetupDatabase(
            8243,
            config => MetadataScopeTestFixture.ConfigureVideoSeed(config, PlexMediaType.Movie, 3)
        );
        var dbContext = IDbContext;
        var fixture = new MetadataScopeTestFixture(dbContext, CancellationToken);
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.PlexServerId).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(3);
        var rows = await fixture.AddVideoMetadataAsync(libraries, "flag");
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
        var expectedLibraries = expectedIndexes.Select(index => libraries[index]).ToList();
        expectedLibraries.Count.ShouldBeGreaterThan(0);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetLibraryMediaMetadataRequest
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
        var response = (
            await ReadMetadataResponseAsync(endpointResult.Endpoint.HttpContext.Response.Body)
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var metadata = response.Value.ShouldNotBeNull();
        metadata.MediaCount.ShouldBe(expectedLibraries.Count);
        metadata.RoleCount.ShouldBe(expectedLibraries.Count);
        metadata.CountryCount.ShouldBe(expectedLibraries.Count);
        metadata.GenreCount.ShouldBe(expectedLibraries.Count);
        metadata.QualityCount.ShouldBe(1);
        metadata
            .Roles.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ShouldBe(expectedIndexes.Select(index => rows.Actors[index].Id).Order());
        metadata
            .Countries.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ShouldBe(expectedIndexes.Select(index => rows.Countries[index].Id).Order());
        metadata
            .Genres.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ShouldBe(expectedIndexes.Select(index => rows.Genres[index].Id).Order());
        metadata
            .Qualities.OrderBy(x => x.Quality)
            .Select(x => (x.Quality, x.Count))
            .ShouldBe([(VideoQuality.FullHD, expectedLibraries.Count)]);
    }

    [Test]
    public async Task ShouldIncludeEveryEnabledLibrary_WhenNoPlexAccountsExist()
    {
        // Arrange
        await SetupDatabase(8244, MetadataScopeTestFixture.ConfigureNoAccountVideoSeed);
        var dbContext = IDbContext;
        var fixture = new MetadataScopeTestFixture(dbContext, CancellationToken);
        var libraries = await dbContext
            .PlexLibraries.IgnoreQueryFilters()
            .OrderBy(x => x.PlexServerId)
            .ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        (await dbContext.PlexAccounts.CountAsync(CancellationToken)).ShouldBe(0);
        var rows = await fixture.AddVideoMetadataAsync(libraries, "fallback");
        await fixture.SetVideoQualitiesAsync(
            PlexMediaType.Movie,
            libraries,
            [VideoQuality.FullHD, VideoQuality.UHD_4K]
        );
        await fixture.DisableLibraryAsync(libraries[1]);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetLibraryMediaMetadataRequest { PlexLibraryId = 0, MediaType = PlexMediaType.Movie }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        var response = (
            await ReadMetadataResponseAsync(endpointResult.Endpoint.HttpContext.Response.Body)
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var metadata = response.Value.ShouldNotBeNull();
        metadata.MediaCount.ShouldBe(1);
        metadata.RoleCount.ShouldBe(1);
        metadata.CountryCount.ShouldBe(1);
        metadata.GenreCount.ShouldBe(1);
        metadata.QualityCount.ShouldBe(1);
        metadata.Roles.Select(x => x.Id).ShouldBe([rows.Actors[0].Id]);
        metadata.Countries.Select(x => x.Id).ShouldBe([rows.Countries[0].Id]);
        metadata.Genres.Select(x => x.Id).ShouldBe([rows.Genres[0].Id]);
        metadata.Qualities.Select(x => (x.Quality, x.Count)).ShouldBe([(VideoQuality.FullHD, 1)]);
    }

    [Test]
    public async Task ShouldUseAudioGenresFromAllowedLibraries_WhenAllLibrariesAreRequested()
    {
        // Arrange
        await SetupDatabase(
            8245,
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
        var audioGenres = await fixture.AddAudioGenresAsync(libraries, "audio-scope");
        var account = await dbContext.PlexAccounts.SingleAsync(CancellationToken);
        await fixture.RevokeLibraryAccessAsync(account, libraries[1]);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetLibraryMediaMetadataRequest { PlexLibraryId = 0, MediaType = PlexMediaType.MusicArtist }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        var response = (
            await ReadMetadataResponseAsync(endpointResult.Endpoint.HttpContext.Response.Body)
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var metadata = response.Value.ShouldNotBeNull();
        metadata.MediaCount.ShouldBe(1);
        metadata.RoleCount.ShouldBe(0);
        metadata.CountryCount.ShouldBe(0);
        metadata.GenreCount.ShouldBe(1);
        metadata.QualityCount.ShouldBe(0);
        metadata.Roles.ShouldBeEmpty();
        metadata.Countries.ShouldBeEmpty();
        metadata.Genres.Select(x => (x.Id, x.Name)).ShouldBe([(audioGenres[0].Id, audioGenres[0].Name)]);
        metadata.Qualities.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies metadata publishes only its root media type's supported sorts, including empty or disabled scopes.
    /// </summary>
    /// <remarks>
    /// Assertions inspect the serialized response so an incorrect object-shaped catalog, omitted field,
    /// or incorrect enum wire values cannot be concealed by deserialization defaults.
    /// </remarks>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldPublishFamilySortCapabilities_WhenMetadataScopeIsEmptyOrDisabled(
        bool allLibraries,
        bool disabled
    )
    {
        // Arrange
        await SetupDatabase(
            8250,
            config =>
            {
                MetadataScopeTestFixture.ConfigureVideoSeed(config, PlexMediaType.Movie, 1);
                config.MovieCount = 0;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync();
        if (disabled)
            await new MetadataScopeTestFixture(dbContext, CancellationToken).DisableLibraryAsync(library);

        var expected = new[]
        {
            "title",
            "year",
            "addedAt",
            "updatedAt",
            "duration",
            "mediaSize",
            "videoQuality",
            "comparisonState",
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetLibraryMediaMetadataRequest
            {
                PlexLibraryId = allLibraries ? 0 : library.Id,
                MediaType = PlexMediaType.Movie,
            }
        );
        endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
        var root = await System.Text.Json.Nodes.JsonNode.ParseAsync(
            endpointResult.Endpoint.HttpContext.Response.Body,
            new System.Text.Json.Nodes.JsonNodeOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: CancellationToken
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        root.ShouldNotBeNull();
        root["isSuccess"]!.GetValue<bool>().ShouldBeTrue();
        root["errors"]!.AsArray().Count.ShouldBe(0);
        var capabilities = root["value"]!["supportedSorts"]!.AsArray();
        capabilities.Select(field => field!.GetValue<string>()).ShouldBe(expected);
    }

    private async Task<ResultDTO<PlexMediaMetadataDTO>?> ReadMetadataResponseAsync(Stream body)
    {
        body.Position = 0;
        return await System.Text.Json.JsonSerializer.DeserializeAsync<ResultDTO<PlexMediaMetadataDTO>>(
            body,
            DefaultJsonSerializerOptions.ConfigStandard,
            CancellationToken
        );
    }
}
