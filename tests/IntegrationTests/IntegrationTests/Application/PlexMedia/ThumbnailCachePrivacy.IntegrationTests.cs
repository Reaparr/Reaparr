using System.Net.Http.Headers;
using System.Net.Http.Json;
using Moq.Contrib.HttpClient;

namespace Reaparr.IntegrationTests;

public class ThumbnailCachePrivacyIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPreventSharedCachingAndPreserveAuthenticationPolicy_AcrossBrowserSessions(
        bool useCookieAuthentication
    )
    {
        // Arrange
        var seed = new Seed(17021);
        var upstreamRequests = new List<Uri>();
        byte[] image = [1, 2, 3];
        using var container = await BaseContainer.Create(
            LogFactory.Create<ThumbnailCachePrivacyIntegrationTests>(),
            seed,
            config =>
            {
                config.DatabaseOptions = database => database.PlexAccountCount = 1;
                config.HttpClientOptions = (handler, _) =>
                {
                    handler.SetupIdentityRequest(seed);
                    handler
                        .SetupRequest(request =>
                            request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/photo/:/transcode"
                        )
                        .ReturnsAsync(
                            (HttpRequestMessage request, CancellationToken _) =>
                            {
                                upstreamRequests.Add(request.RequestUri!);
                                var response = new HttpResponseMessage(HttpStatusCode.OK)
                                {
                                    Content = new ByteArrayContent(image),
                                };
                                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                                return response;
                            }
                        );
                };
            },
            useTestAuthentication: !useCookieAuthentication
        );
        var serverId = await container.DbContext.PlexServers.Select(server => server.Id).SingleAsync(CancellationToken);
        var path =
            $"{ApiRoutes.PlexMediaController}/thumbnail?plexServerId={serverId}&plexKey=123&metaDataKey=456&width=200&height=300";
        using var firstBrowser = container.GetApiClient();
        if (useCookieAuthentication)
        {
            firstBrowser.DefaultRequestHeaders.Remove("Authorization");
            using var firstLogin = new MultipartFormDataContent
            {
                { new StringContent(DefaultUserAppCredentials.DefaultUsername), "Username" },
                { new StringContent(DefaultUserAppCredentials.DefaultPassword), "Password" },
            };
            using var firstLoginResponse = await firstBrowser.PostAsync(
                ApiRoutes.LoginEndpoint,
                firstLogin,
                CancellationToken
            );
            firstLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            firstBrowser.DefaultRequestHeaders.Add("Cookie", "browser=first");
        }

        // Act
        using var imageResponse = await firstBrowser.GetAsync(path, CancellationToken);

        // Assert
        imageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        imageResponse.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await imageResponse.Content.ReadAsByteArrayAsync(CancellationToken)).ShouldBe(image);
        var cacheControl = imageResponse.Headers.CacheControl.ShouldNotBeNull();
        cacheControl.Public.ShouldBeFalse();
        cacheControl.NoStore.ShouldBe(useCookieAuthentication);
        if (useCookieAuthentication)
        {
            // Security-stamp validation renews the cookie and intentionally forbids caching.
            cacheControl.NoCache.ShouldBeTrue();
        }
        else
        {
            cacheControl.Private.ShouldBeTrue();
            cacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(3));
        }
        imageResponse.Headers.Vary.ShouldContain("Cookie");
        var etag = imageResponse.Headers.ETag.ShouldNotBeNull();
        upstreamRequests.Count.ShouldBe(1);
        var upstreamQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(upstreamRequests[0].Query);
        upstreamQuery["width"].ToString().ShouldBe("200");
        upstreamQuery["height"].ToString().ShouldBe("300");
        upstreamQuery["url"].ToString().ShouldStartWith("/library/metadata/123/thumb/456?");

        using var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, path);
        conditionalRequest.Headers.IfNoneMatch.Add(etag);
        using var conditionalResponse = await firstBrowser.SendAsync(conditionalRequest, CancellationToken);
        conditionalResponse.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await conditionalResponse.Content.ReadAsByteArrayAsync(CancellationToken)).ShouldBeEmpty();
        var conditionalCacheControl = conditionalResponse.Headers.CacheControl.ShouldNotBeNull();
        conditionalCacheControl.Public.ShouldBeFalse();
        conditionalCacheControl.NoStore.ShouldBe(useCookieAuthentication);
        if (!useCookieAuthentication)
        {
            conditionalCacheControl.Private.ShouldBeTrue();
            conditionalCacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(3));
        }
        conditionalResponse.Headers.Vary.ShouldContain("Cookie");
        conditionalResponse.Headers.ETag.ShouldBe(etag);
        upstreamRequests.Count.ShouldBe(1);

        if (useCookieAuthentication)
        {
            using var anonymousBrowser = container.GetApiClient();
            anonymousBrowser.DefaultRequestHeaders.Remove("Authorization");
            using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, path);
            anonymousRequest.Headers.IfNoneMatch.Add(etag);
            using var anonymousResponse = await anonymousBrowser.SendAsync(anonymousRequest, CancellationToken);
            anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            upstreamRequests.Count.ShouldBe(1);
        }

        using var secondBrowser = container.GetApiClient();
        if (useCookieAuthentication)
        {
            secondBrowser.DefaultRequestHeaders.Remove("Authorization");
            using var secondLogin = new MultipartFormDataContent
            {
                { new StringContent(DefaultUserAppCredentials.DefaultUsername), "Username" },
                { new StringContent(DefaultUserAppCredentials.DefaultPassword), "Password" },
            };
            using var secondLoginResponse = await secondBrowser.PostAsync(
                ApiRoutes.LoginEndpoint,
                secondLogin,
                CancellationToken
            );
            secondLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            secondBrowser.DefaultRequestHeaders.Add("Cookie", "browser=second");
        }
        using var secondImageResponse = await secondBrowser.GetAsync(path, CancellationToken);
        secondImageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await secondImageResponse.Content.ReadAsByteArrayAsync(CancellationToken)).ShouldBe(image);
        var secondCacheControl = secondImageResponse.Headers.CacheControl.ShouldNotBeNull();
        secondCacheControl.Public.ShouldBeFalse();
        secondCacheControl.NoStore.ShouldBe(useCookieAuthentication);
        if (!useCookieAuthentication)
        {
            secondCacheControl.Private.ShouldBeTrue();
            secondCacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(3));
        }
        secondImageResponse.Headers.Vary.ShouldContain("Cookie");
        upstreamRequests.Count.ShouldBe(2);
        upstreamRequests[1].ShouldBe(upstreamRequests[0]);
    }

    [Test]
    [Arguments(200, "image/svg+xml", 502, false)]
    [Arguments(200, "image/svg+xml", 502, true)]
    [Arguments(404, "image/png", 404, false)]
    [Arguments(404, "image/png", 404, true)]
    [Arguments(401, "image/png", 601, false)]
    [Arguments(401, "image/png", 601, true)]
    public async Task ShouldKeepThumbnailFailuresNonCacheable_WhenPlexRejectsTheImage(
        int upstreamStatus,
        string upstreamContentType,
        int expectedStatus,
        bool useCookieAuthentication
    )
    {
        // Arrange
        var seed = new Seed(17022);
        var upstreamRequests = new List<string>();
        using var container = await BaseContainer.Create(
            LogFactory.Create<ThumbnailCachePrivacyIntegrationTests>(),
            seed,
            config =>
            {
                config.DatabaseOptions = database => database.PlexAccountCount = 1;
                config.HttpClientOptions = (handler, _) =>
                {
                    handler.SetupIdentityRequest(seed);
                    handler
                        .SetupRequest(request =>
                            request.Method == HttpMethod.Get
                            && (
                                request.RequestUri!.AbsolutePath == "/photo/:/transcode"
                                || request.RequestUri.AbsolutePath == "/library/metadata/123/thumb/456"
                            )
                        )
                        .ReturnsAsync(
                            (HttpRequestMessage request, CancellationToken _) =>
                            {
                                upstreamRequests.Add(request.RequestUri!.AbsolutePath);
                                var response = new HttpResponseMessage((HttpStatusCode)upstreamStatus)
                                {
                                    Content = new StringContent("rejected-upstream-content"),
                                };
                                response.Content.Headers.ContentType = new MediaTypeHeaderValue(upstreamContentType);
                                return response;
                            }
                        );
                };
            },
            useTestAuthentication: !useCookieAuthentication
        );
        var serverId = await container.DbContext.PlexServers.Select(server => server.Id).SingleAsync(CancellationToken);
        using var browser = container.GetApiClient();
        if (useCookieAuthentication)
        {
            browser.DefaultRequestHeaders.Remove("Authorization");
            using var login = new MultipartFormDataContent
            {
                { new StringContent(DefaultUserAppCredentials.DefaultUsername), "Username" },
                { new StringContent(DefaultUserAppCredentials.DefaultPassword), "Password" },
            };
            using var loginResponse = await browser.PostAsync(ApiRoutes.LoginEndpoint, login, CancellationToken);
            loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Act
        using var response = await browser.GetAsync(
            $"{ApiRoutes.PlexMediaController}/thumbnail?plexServerId={serverId}&plexKey=123&metaDataKey=456&width=200&height=300",
            CancellationToken
        );

        // Assert
        response.StatusCode.ShouldBe((HttpStatusCode)expectedStatus);
        var cacheControl = response.Headers.CacheControl.ShouldNotBeNull();
        cacheControl.NoStore.ShouldBeTrue();
        cacheControl.Public.ShouldBeFalse();
        var result = (await response.Content.ReadFromJsonAsync<BaseResultDTO>(CancellationToken)).ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(expectedStatus);
        result.Errors.Count.ShouldBe(1);
        (await response.Content.ReadAsStringAsync(CancellationToken)).ShouldNotContain("rejected-upstream-content");
        upstreamRequests.ShouldBe(
            upstreamStatus == 404
                ? new[] { "/photo/:/transcode", "/library/metadata/123/thumb/456" }
                : new[] { "/photo/:/transcode" }
        );
    }
}
