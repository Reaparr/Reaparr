using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Application.UnitTests;

public class GetPhotoAssetImageEndpointUnitTests
    : BaseEndpointUnitTest<GetPhotoAssetImageEndpoint, GetPhotoAssetImageRequest, byte[]>
{
    [Test]
    public async Task ShouldStreamOriginalPartWithoutExposingSourcePath()
    {
        // Arrange
        await SetupDatabase(
            84001,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        const string sourcePath = "/private/plex/photos/secret/original.jpg";
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SourcePath, sourcePath), CancellationToken);
        var payload = new byte[] { 1, 2, 3, 4 };
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }
        );
        handler.Responses[0].Content.Headers.ContentType = new("image/jpeg");
        handler.Responses[0].Content.Headers.ContentLength = payload.Length + 100;
        var clientFactory = CreateClientFactory(handler);

        // Act
        var result = await TestEndpointHandleAsync(
            new GetPhotoAssetImageRequest
            {
                PlexLibraryId = asset.PlexLibraryId,
                AssetId = asset.Id,
                Representation = PhotoImageRepresentation.Original,
            },
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.ContentType.ShouldBe("image/jpeg");
        result.Endpoint.HttpContext.Response.ContentLength.ShouldBeNull();
        ((MemoryStream)result.Endpoint.HttpContext.Response.Body).ToArray().ShouldBe(payload);
        var cacheControl = result.Endpoint.HttpContext.Response.GetTypedHeaders().CacheControl;
        cacheControl.ShouldNotBeNull();
        cacheControl.Private.ShouldBeTrue();
        cacheControl.Public.ShouldBeFalse();
        cacheControl.NoStore.ShouldBeFalse();
        cacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(3));
        result.Endpoint.HttpContext.Response.Headers.Vary.ToString().ShouldBe("Cookie");
        handler.RequestUris[0].AbsolutePath.ShouldBe(asset.Key);
        handler.RequestUris[0].Query.ShouldContain("X-Plex-Token=");
        handler.RequestUris[0].ToString().ShouldNotContain(sourcePath);
        clientFactory.Verify(x => x.CreateClient(HttpClientModule.PlexThumbnailClientName), Times.Once);
    }

    [Test]
    public async Task ShouldConvertFromOriginalPartWithNativeDimensionsAndTransparentFormat()
    {
        // Arrange
        await SetupDatabase(
            84002,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        const string sourcePath = "/private/plex/photos/secret/transparent.png";
        var partKey = $"/library/parts/{asset.PlexApiPartId}/file.png";
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SourcePath, sourcePath)
                        .SetProperty(y => y.Key, partKey)
                        .SetProperty(y => y.Container, "png")
                        .SetProperty(y => y.Width, 6000)
                        .SetProperty(y => y.Height, 4000),
                CancellationToken
            );
        dbContext.ClearChangeTracker();
        var payload = new byte[] { 9, 8, 7 };
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }
        );
        handler.Responses[0].Content.Headers.ContentType = new("image/png");
        var clientFactory = CreateClientFactory(handler);

        // Act
        var result = await TestEndpointHandleAsync(
            new GetPhotoAssetImageRequest
            {
                PlexLibraryId = asset.PlexLibraryId,
                AssetId = asset.Id,
                Representation = PhotoImageRepresentation.Converted,
            },
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.ContentType.ShouldBe("image/png");
        ((MemoryStream)result.Endpoint.HttpContext.Response.Body).ToArray().ShouldBe(payload);
        handler.RequestUris.Count.ShouldBe(1);
        var uri = handler.RequestUris[0];
        uri.AbsolutePath.ShouldBe("/photo/:/transcode");
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["format"].ToString().ShouldBe("png");
        query["width"].ToString().ShouldBe("6000");
        query["height"].ToString().ShouldBe("4000");
        query["quality"].ToString().ShouldBe("-1");
        query["upscale"].ToString().ShouldBe("0");
        query["minSize"].ToString().ShouldBe("0");
        query["rotate"].ToString().ShouldBe("1");
        query["url"].ToString().ShouldContain(partKey);
        query["url"].ToString().ShouldContain("X-Plex-Token=");
        uri.ToString().ShouldNotContain(sourcePath);
        clientFactory.Verify(x => x.CreateClient(HttpClientModule.PlexThumbnailClientName), Times.Once);
    }

    [Test]
    public async Task ShouldKeepNativeOriginalAvailableButRejectConversion_WhenDimensionsAreAbsent()
    {
        // Arrange
        await SetupDatabase(
            84003,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.Width, (int?)null).SetProperty(y => y.Height, (int?)null),
                CancellationToken
            );
        dbContext.ClearChangeTracker();
        var conversionHandler = new StubHttpMessageHandler();
        var conversionFactory = CreateClientFactory(conversionHandler, expectedCalls: 0);

        // Act
        var converted = await TestEndpointHandleAsync(
            new GetPhotoAssetImageRequest
            {
                PlexLibraryId = asset.PlexLibraryId,
                AssetId = asset.Id,
                Representation = PhotoImageRepresentation.Converted,
            },
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(conversionFactory.Object)
        );
        var originalHandler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([5, 6]) }
        );
        originalHandler.Responses[0].Content.Headers.ContentType = new("image/jpeg");
        var originalFactory = CreateClientFactory(originalHandler);
        var original = await TestEndpointHandleAsync(
            new GetPhotoAssetImageRequest
            {
                PlexLibraryId = asset.PlexLibraryId,
                AssetId = asset.Id,
                Representation = PhotoImageRepresentation.Original,
            },
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(originalFactory.Object)
        );

        // Assert
        converted.IsValid.ShouldBeTrue();
        converted.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        conversionHandler.RequestUris.ShouldBeEmpty();
        original.IsValid.ShouldBeTrue();
        original.StatusCode.ShouldBe(StatusCodes.Status200OK);
        original.ContentType.ShouldBe("image/jpeg");
        ((MemoryStream)original.Endpoint.HttpContext.Response.Body).ToArray().ShouldBe([5, 6]);
        originalHandler.RequestUris.Count.ShouldBe(1);
        conversionFactory.Verify();
        originalFactory.Verify();
    }

    [Test]
    public async Task ShouldRejectWrongKindAndForeignScopeBeforeRequestingPlex()
    {
        // Arrange
        await SetupDatabase(
            84004,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
                config.PhotoClipCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var clip = await dbContext.PlexPhotoMediaData.SingleAsync(
            x => x.PlexLibraryId == libraries[0].Id && x.AssetKind == PhotoAssetKind.VideoClip,
            CancellationToken
        );
        var foreignAsset = await dbContext.PlexPhotoMediaData.FirstAsync(
            x => x.PlexLibraryId == libraries[1].Id && x.AssetKind == PhotoAssetKind.Image,
            CancellationToken
        );
        var handler = new StubHttpMessageHandler();
        var clientFactory = CreateClientFactory(handler, expectedCalls: 0);

        // Act
        var wrongKind = await TestEndpointHandleAsync(
            new GetPhotoAssetImageRequest
            {
                PlexLibraryId = clip.PlexLibraryId,
                AssetId = clip.Id,
                Representation = PhotoImageRepresentation.Original,
            },
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );
        var foreignScope = await TestEndpointHandleAsync(
            new GetPhotoAssetImageRequest
            {
                PlexLibraryId = libraries[0].Id,
                AssetId = foreignAsset.Id,
                Representation = PhotoImageRepresentation.Original,
            },
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );

        // Assert
        wrongKind.IsValid.ShouldBeTrue();
        wrongKind.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        foreignScope.IsValid.ShouldBeTrue();
        foreignScope.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        handler.RequestUris.ShouldBeEmpty();
        clientFactory.Verify();
    }

    [Test]
    public async Task ShouldRejectDisabledLibraryBeforeRequestingPlex()
    {
        // Arrange
        await SetupDatabase(
            84005,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        await dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.Id == asset.PlexLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        dbContext.ClearChangeTracker();
        var handler = new StubHttpMessageHandler();
        var clientFactory = CreateClientFactory(handler, expectedCalls: 0);

        // Act
        var result = await TestEndpointHandleAsync(
            new GetPhotoAssetImageRequest
            {
                PlexLibraryId = asset.PlexLibraryId,
                AssetId = asset.Id,
                Representation = PhotoImageRepresentation.Original,
            },
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        handler.RequestUris.ShouldBeEmpty();
        clientFactory.Verify();
    }

    [Test]
    [Arguments(HttpStatusCode.NotFound, StatusCodes.Status502BadGateway)]
    [Arguments(HttpStatusCode.Unauthorized, HttpCodes.Status601PlexAuthenticationFailed)]
    [Arguments(HttpStatusCode.Forbidden, HttpCodes.Status601PlexAuthenticationFailed)]
    [Arguments(HttpStatusCode.InternalServerError, StatusCodes.Status502BadGateway)]
    public async Task ShouldNotExposeCredentialsOrPaths_WhenPlexFails(HttpStatusCode upstreamStatus, int expectedStatus)
    {
        // Arrange
        await SetupDatabase(
            84006,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        const string sourcePath = "/private/plex/photos/secret/failure.jpg";
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SourcePath, sourcePath), CancellationToken);
        var token = (await dbContext.GetPlexServerTokenAsync(asset.PlexServerId, CancellationToken)).Value;
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(upstreamStatus) { Content = new StringContent($"{token} {sourcePath}") }
        );
        var clientFactory = CreateClientFactory(handler);

        // Act
        var result = await TestEndpointHandleAsync(
            new GetPhotoAssetImageRequest
            {
                PlexLibraryId = asset.PlexLibraryId,
                AssetId = asset.Id,
                Representation = PhotoImageRepresentation.Original,
            },
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(expectedStatus);
        result.Endpoint.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        result.Endpoint.HttpContext.Response.Headers.ETag.ToString().ShouldBeEmpty();
        var responseText = System.Text.Encoding.UTF8.GetString(
            ((MemoryStream)result.Endpoint.HttpContext.Response.Body).ToArray()
        );
        responseText.ShouldNotContain(token);
        responseText.ShouldNotContain(sourcePath);
        responseText.ShouldNotContain(asset.Key);
        clientFactory.Verify();
    }

    [Test]
    public async Task ShouldRejectUnknownRepresentation()
    {
        // Arrange
        var request = new GetPhotoAssetImageRequest
        {
            PlexLibraryId = 1,
            AssetId = 1,
            Representation = (PhotoImageRepresentation)99,
        };

        // Act
        var result = await TestEndpointHandleAsync(
            request,
            services =>
                services.AddMemoryCache().AddSingleton<IHttpClientFactory>(Mock.Mock<IHttpClientFactory>().Object)
        );

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
    }

    [Test]
    [Arguments("")]
    [Arguments("W/")]
    [Arguments("\"unrelated\", ")]
    [Arguments("*")]
    public async Task ShouldReturnNotModifiedWithoutRequestingPlex_WhenETagMatches(string etagPrefix)
    {
        // Arrange
        await SetupDatabase(
            84007,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }
        );
        handler.Responses[0].Content.Headers.ContentType = new("image/jpeg");
        var clientFactory = CreateClientFactory(handler);
        var request = new GetPhotoAssetImageRequest
        {
            PlexLibraryId = asset.PlexLibraryId,
            AssetId = asset.Id,
            Representation = PhotoImageRepresentation.Original,
        };
        var initial = await TestEndpointHandleAsync(
            request,
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );
        initial.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var etag = initial.Endpoint.HttpContext.Response.Headers.ETag.ToString();
        var endpoint = initial.Endpoint;
        endpoint.HttpContext.Response.Clear();
        endpoint.HttpContext.Request.Headers.IfNoneMatch = etagPrefix == "*" ? "*" : $"{etagPrefix}{etag}";

        // Act
        await endpoint.HandleAsync(request, CancellationToken);

        // Assert
        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        ((MemoryStream)endpoint.HttpContext.Response.Body).ToArray().ShouldBe(Array.Empty<byte>());
        endpoint.HttpContext.Response.Headers.ETag.ToString().ShouldBe(etag);
        endpoint.HttpContext.Response.GetTypedHeaders().CacheControl!.Private.ShouldBeTrue();
        endpoint.HttpContext.Response.GetTypedHeaders().CacheControl!.MaxAge.ShouldBe(TimeSpan.FromDays(3));
        endpoint.HttpContext.Response.Headers.Vary.ToString().ShouldBe("Cookie");
        handler.RequestUris.Count.ShouldBe(1);
        clientFactory.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnNewImage_WhenMetadataOrRepresentationChanges(bool changeRepresentation)
    {
        // Arrange
        await SetupDatabase(
            84008,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.Width, 6000).SetProperty(y => y.Height, 4000),
                CancellationToken
            );
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2]) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([3, 4]) }
        );
        foreach (var response in handler.Responses)
            response.Content.Headers.ContentType = new("image/jpeg");
        var clientFactory = CreateClientFactory(handler, expectedCalls: 2);
        var request = new GetPhotoAssetImageRequest
        {
            PlexLibraryId = asset.PlexLibraryId,
            AssetId = asset.Id,
            Representation = PhotoImageRepresentation.Original,
        };
        var initial = await TestEndpointHandleAsync(
            request,
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );
        initial.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var originalETag = initial.Endpoint.HttpContext.Response.Headers.ETag.ToString();
        if (changeRepresentation)
            request = request with { Representation = PhotoImageRepresentation.Converted };
        else
        {
            var updatedAt = (asset.UpdatedAt ?? asset.AddedAt).AddSeconds(1);
            await dbContext
                .PlexPhotoMediaData.Where(x => x.Id == asset.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.UpdatedAt, updatedAt), CancellationToken);
        }
        var endpoint = initial.Endpoint;
        endpoint.HttpContext.Response.Clear();
        endpoint.HttpContext.Request.Headers.IfNoneMatch = originalETag;

        // Act
        await endpoint.HandleAsync(request, CancellationToken);

        // Assert
        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ((MemoryStream)endpoint.HttpContext.Response.Body).ToArray().ShouldBe([3, 4]);
        endpoint.HttpContext.Response.Headers.ETag.ToString().ShouldNotBe(originalETag);
        handler
            .RequestUris.Select(x => x.AbsolutePath)
            .ShouldBe([asset.Key, changeRepresentation ? "/photo/:/transcode" : asset.Key]);
        clientFactory.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldRejectConditionalRequest_WhenAssetIsDeletedOrLibraryIsDisabled(bool disableLibrary)
    {
        // Arrange
        await SetupDatabase(
            84009,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2]) }
        );
        handler.Responses[0].Content.Headers.ContentType = new("image/jpeg");
        var clientFactory = CreateClientFactory(handler);
        var request = new GetPhotoAssetImageRequest
        {
            PlexLibraryId = asset.PlexLibraryId,
            AssetId = asset.Id,
            Representation = PhotoImageRepresentation.Original,
        };
        var initial = await TestEndpointHandleAsync(
            request,
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );
        initial.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var etag = initial.Endpoint.HttpContext.Response.Headers.ETag.ToString();
        if (disableLibrary)
            await dbContext
                .PlexLibraries.IgnoreIsEnabledFilter()
                .Where(x => x.Id == asset.PlexLibraryId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        else
            await dbContext.PlexPhotoMediaData.Where(x => x.Id == asset.Id).ExecuteDeleteAsync(CancellationToken);
        var endpoint = initial.Endpoint;
        endpoint.HttpContext.Response.Clear();
        endpoint.HttpContext.Request.Headers.IfNoneMatch = etag;

        // Act
        await endpoint.HandleAsync(request, CancellationToken);

        // Assert
        endpoint.HttpContext.Response.StatusCode.ShouldBe(
            disableLibrary ? StatusCodes.Status400BadRequest : StatusCodes.Status404NotFound
        );
        endpoint.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        endpoint.HttpContext.Response.Headers.ETag.ToString().ShouldBeEmpty();
        handler.RequestUris.Count.ShouldBe(1);
        clientFactory.Verify();
    }

    [Test]
    public async Task ShouldReuseSuccessfulConnectionUntilCacheEntryIsEvicted()
    {
        // Arrange
        await SetupDatabase(
            84010,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var originalConnection = (
            await dbContext.ChoosePlexServerConnection(asset.PlexServerId, CancellationToken)
        ).Value;
        const string replacementUrl = "https://replacement.plex.example:32400";
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([2]) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([3]) }
        );
        foreach (var response in handler.Responses)
            response.Content.Headers.ContentType = new("image/jpeg");
        var clientFactory = CreateClientFactory(handler, expectedCalls: 3);
        var request = new GetPhotoAssetImageRequest
        {
            PlexLibraryId = asset.PlexLibraryId,
            AssetId = asset.Id,
            Representation = PhotoImageRepresentation.Original,
        };
        var initial = await TestEndpointHandleAsync(
            request,
            services =>
                services.AddSingleton<IMemoryCache>(cache).AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );
        initial.StatusCode.ShouldBe(StatusCodes.Status200OK);
        await dbContext
            .PlexServerConnections.Where(x => x.PlexServerId == asset.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Url, replacementUrl), CancellationToken);
        var endpoint = initial.Endpoint;
        endpoint.HttpContext.Response.Clear();

        // Act
        await endpoint.HandleAsync(request, CancellationToken);
        var cachedStatus = endpoint.HttpContext.Response.StatusCode;
        var cachedPayload = ((MemoryStream)endpoint.HttpContext.Response.Body).ToArray();
        cache.Remove($"plex_connection_{asset.PlexServerId}");
        endpoint.HttpContext.Response.Clear();
        await endpoint.HandleAsync(request, CancellationToken);

        // Assert
        cachedStatus.ShouldBe(StatusCodes.Status200OK);
        cachedPayload.ShouldBe([2]);
        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ((MemoryStream)endpoint.HttpContext.Response.Body).ToArray().ShouldBe([3]);
        handler
            .RequestUris.Select(x => x.GetLeftPart(UriPartial.Authority))
            .ShouldBe([
                new Uri(originalConnection.Url).GetLeftPart(UriPartial.Authority),
                new Uri(originalConnection.Url).GetLeftPart(UriPartial.Authority),
                replacementUrl,
            ]);
        clientFactory.Verify();
    }

    [Test]
    public async Task ShouldRetryConnectionSelection_WhenAnUncachedFailureBecomesAvailable()
    {
        // Arrange
        await SetupDatabase(
            84011,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        await dbContext
            .PlexServerStatuses.Where(x => x.PlexServerId == asset.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSuccessful, false), CancellationToken);
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([7, 8]) }
        );
        handler.Responses[0].Content.Headers.ContentType = new("image/jpeg");
        var clientFactory = CreateClientFactory(handler);
        var request = new GetPhotoAssetImageRequest
        {
            PlexLibraryId = asset.PlexLibraryId,
            AssetId = asset.Id,
            Representation = PhotoImageRepresentation.Original,
        };
        var unavailable = await TestEndpointHandleAsync(
            request,
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );
        unavailable.StatusCode.ShouldBe(StatusCodes.Status504GatewayTimeout);
        unavailable.Endpoint.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        unavailable.Endpoint.HttpContext.Response.Headers.ETag.ToString().ShouldBeEmpty();
        handler.RequestUris.ShouldBeEmpty();
        await dbContext
            .PlexServerStatuses.Where(x => x.PlexServerId == asset.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSuccessful, true), CancellationToken);
        var endpoint = unavailable.Endpoint;
        endpoint.HttpContext.Response.Clear();

        // Act
        await endpoint.HandleAsync(request, CancellationToken);

        // Assert
        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ((MemoryStream)endpoint.HttpContext.Response.Body).ToArray().ShouldBe([7, 8]);
        handler.RequestUris.Count.ShouldBe(1);
        clientFactory.Verify();
    }

    [Test]
    public async Task ShouldRemoveSuccessfulCacheHeaders_WhenReadingTheUpstreamImageFails()
    {
        // Arrange
        await SetupDatabase(
            84012,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnreadableImageContent() }
        );
        handler.Responses[0].Content.Headers.ContentType = new("image/jpeg");
        var clientFactory = CreateClientFactory(handler);

        // Act
        var result = await TestEndpointHandleAsync(
            new GetPhotoAssetImageRequest
            {
                PlexLibraryId = asset.PlexLibraryId,
                AssetId = asset.Id,
                Representation = PhotoImageRepresentation.Original,
            },
            services => services.AddMemoryCache().AddSingleton<IHttpClientFactory>(clientFactory.Object)
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        result.Endpoint.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        result.Endpoint.HttpContext.Response.Headers.ETag.ToString().ShouldBeEmpty();
        handler.RequestUris.Count.ShouldBe(1);
        clientFactory.Verify();
    }

    private sealed class UnreadableImageContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.FromException(new IOException("The upstream image stream failed."));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private static Mock<IHttpClientFactory> CreateClientFactory(StubHttpMessageHandler handler, int expectedCalls = 1)
    {
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factory
            .Setup(x => x.CreateClient(HttpClientModule.PlexThumbnailClientName))
            .Returns(client)
            .Verifiable(Times.Exactly(expectedCalls));
        return factory;
    }

    private sealed class StubHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<Uri> RequestUris { get; } = [];
        public IReadOnlyList<HttpResponseMessage> Responses { get; } = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
