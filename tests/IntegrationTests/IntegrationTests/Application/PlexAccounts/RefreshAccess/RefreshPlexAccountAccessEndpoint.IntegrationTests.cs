using Moq.Contrib.HttpClient;

namespace Reaparr.IntegrationTests;

public class RefreshPlexAccountAccessEndpointIntegrationTestsIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldRefreshAccessPlexAccountAndNot500_WhenAPlexAccountIsUnauthorized()
    {
        // Arrange
        var plexAccountCount = 2;
        var plexServerCount = 5;
        var plexLibraryCount = 5;
        var seed = new Seed(22453);
        using var container = await CreateContainer(seed, config =>
        {
            config.DatabaseOptions = x =>
            {
                x.PlexAccountCount = plexAccountCount;
                x.PlexServerCount = plexServerCount;
                x.PlexMovieLibraryCount = plexLibraryCount;
            };
            config.BaseMockHttpClientOptions = x =>
            {
                x.PlexServerAccessCount = plexServerCount;
                x.MovieLibraryCount = plexLibraryCount;
                x.MoviesPerLibraryCount = 25;
                x.SetServerResourcesResponse = HttpStatusCode.Unauthorized;
            };
            config.HttpClientOptions = (handler, _) =>
            {
                handler
                    .SetupRequest(HttpMethod.Get, "https://plex.tv/api/v2/user")
                    .ReturnsAsync(
                        (HttpRequestMessage req, CancellationToken _) =>
                            FakePlexApiData
                                .GetPlexUnauthorizedResponseMessage(req)
                                .ToJsonHttpResponse(req, HttpStatusCode.Unauthorized)
                    );
            };
        });

        // Act
        var client = container.GetApiClient();
        await client.SignIn();

        var response = await client.GETAsync<
            RefreshPlexAccountAccessEndpoint,
            RefreshPlexAccountAccessEndpointRequest,
            ResultDTO<List<RefreshPlexAccountAccessRapportDTO>>
        >(new RefreshPlexAccountAccessEndpointRequest());

        // Assert
        var result = response.Result;
        result.StatusCode.ShouldBe(200);
        var rapports = result.Value.ShouldNotBeNull();
        rapports.Count.ShouldBe(plexAccountCount);

        foreach (var rapport in rapports)
        {
            rapport.Access.Count.ShouldBe(plexServerCount);
            foreach (var serverAccess in rapport.Access)
            {
                serverAccess.State.ShouldBe(PlexAccessState.Revoked);
                serverAccess.IsServerOffline.ShouldBeFalse();
                serverAccess.LibraryAccess.Count.ShouldBe(plexLibraryCount);

                foreach (var libraryAccess in serverAccess.LibraryAccess)
                {
                    libraryAccess.State.ShouldBe(PlexAccessState.Revoked);
                    libraryAccess.PlexServerId.ShouldBe(serverAccess.PlexServerId);
                }
            }
        }

        container.DbContext.PlexAccountServers.ToList().Count.ShouldBe(0);
        container.DbContext.PlexAccountLibraries.ToList().Count.ShouldBe(0);
    }
}
