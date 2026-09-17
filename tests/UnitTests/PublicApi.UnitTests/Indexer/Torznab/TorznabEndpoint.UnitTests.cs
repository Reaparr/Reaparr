using Reaparr.Application.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class TorznabEndpointUnitTests : BaseEndpointUnitTest<TorznabEndpoint, TorznabEndpointRequest>
{
    [Test]
    public async Task ShouldDispatchQuerylessGenericRssByRequestedCategories()
    {
        // Arrange
        await SetupDatabase(6520, config => config.RadarrIntegrationCount = 1);
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var request = new TorznabEndpointRequest
        {
            Type = "search",
            Categories = [5030],
            Limit = 10,
            Offset = 2,
            ApiKey = "generic-key",
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetTorznabRssFeedCommand>(command =>
                        !command.IncludeMovies
                        && command.IncludeEpisodes
                        && command.Categories.SequenceEqual(request.Categories)
                        && command.Integration == integration
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SearchResult("tv-rss-result"))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integration);

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var responseBody = endpointResult.Endpoint.HttpContext.Response.Body;
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody, leaveOpen: true);
        var xml = await reader.ReadToEndAsync();
        xml.ShouldContain("tv-rss-result");
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments(2000, 0, true, false)]
    [Arguments(5030, 0, false, true)]
    [Arguments(2000, 5030, true, true)]
    [Arguments(3000, 0, false, false)]
    public async Task ShouldDispatchGenericSearchWithExactMediaFlags_WhenCategoriesAreProvided(
        int firstCategory,
        int secondCategory,
        bool includeMovies,
        bool includeEpisodes
    )
    {
        // Arrange
        await SetupDatabase(6521 + firstCategory + secondCategory, config => config.RadarrIntegrationCount = 1);
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var categories = secondCategory == 0 ? new[] { firstCategory } : new[] { firstCategory, secondCategory };
        var request = new TorznabEndpointRequest
        {
            Type = "search",
            Query = "Silo",
            TvdbId = 336156,
            TmdbId = 458912,
            ImdbId = "tt14688458",
            Categories = categories,
            Limit = 25,
            Offset = 75,
            ApiKey = "generic-key",
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SearchGenericCommand>(command =>
                        command.Query == request.Query
                        && command.TVDB_ID == request.TvdbId
                        && command.TMDB_ID == request.TmdbId
                        && command.IMDB_ID == request.ImdbId
                        && command.IncludeMovies == includeMovies
                        && command.IncludeEpisodes == includeEpisodes
                        && command.Categories.SequenceEqual(request.Categories)
                        && command.Limit == request.Limit
                        && command.Offset == request.Offset
                        && command.Integration == integration
                        && command.TorznabApiKey == request.ApiKey
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SearchResult("generic-result"))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integration);

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var responseBody = endpointResult.Endpoint.HttpContext.Response.Body;
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody, leaveOpen: true);
        var xml = await reader.ReadToEndAsync();
        xml.ShouldContain("generic-result");
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<SearchTvShowCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<SearchMovieCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldReturnTorznabError_WhenGenericCommandFails()
    {
        // Arrange
        await SetupDatabase(6523, config => config.RadarrIntegrationCount = 1);
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var request = new TorznabEndpointRequest
        {
            Type = "search",
            Query = "Missing",
            Limit = 10,
            ApiKey = "generic-key",
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SearchGenericCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<TorznabMediaSearchResponseDTO>("generic search failed"))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integration);

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var response = endpointResult.Endpoint.HttpContext.Response.Body;
        response.Position = 0;
        using var reader = new StreamReader(response, leaveOpen: true);
        var xml = await reader.ReadToEndAsync();
        xml.ShouldContain("code=\"900\"");
        xml.ShouldContain("Indexer request failed");
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnTorznabError_WhenRssCommandFails()
    {
        // Arrange
        await SetupDatabase(6524, config => config.RadarrIntegrationCount = 1);
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var request = new TorznabEndpointRequest { Type = "search", ApiKey = "key" };
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetTorznabRssFeedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<TorznabMediaSearchResponseDTO>("failed"))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integration);

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var response = endpointResult.Endpoint.HttpContext.Response.Body;
        response.Position = 0;
        using var reader = new StreamReader(response, leaveOpen: true);
        var xml = await reader.ReadToEndAsync();
        xml.ShouldContain("code=\"900\"");
        xml.ShouldContain("Indexer request failed");
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnTorznabError_WhenCapabilitiesCommandFails()
    {
        // Arrange
        await SetupDatabase(6525, config => config.RadarrIntegrationCount = 1);
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var request = new TorznabEndpointRequest { Type = "caps", ApiKey = "key" };
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetCapabilitiesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<TorznabCapsResponseDTO>("failed"))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integration);

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var response = endpointResult.Endpoint.HttpContext.Response.Body;
        response.Position = 0;
        using var reader = new StreamReader(response, leaveOpen: true);
        var xml = await reader.ReadToEndAsync();
        xml.ShouldContain("code=\"900\"");
        xml.ShouldContain("Indexer request failed");
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldStopWritingResponse_WhenRssCommandIsCancelled()
    {
        // Arrange
        await SetupDatabase(6526, config => config.RadarrIntegrationCount = 1);
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var request = new TorznabEndpointRequest { Type = "search", ApiKey = "key" };
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetTorznabRssFeedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ResultExtensions
                    .TaskIsCancelled(nameof(GetTorznabRssFeedCommand))
                    .ToResult<TorznabMediaSearchResponseDTO>()
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integration);

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Endpoint.HttpContext.Response.Body.Length.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    private static Result<TorznabMediaSearchResponseDTO> SearchResult(params string[] titles) =>
        Result.Ok(
            new TorznabMediaSearchResponseDTO
            {
                Channel = new TorznabChannel
                {
                    Items =
                    [
                        .. titles.Select(
                            (title, index) =>
                                new TorznabItem
                                {
                                    Title = title,
                                    PubDate = DateTimeOffset.UnixEpoch.AddMinutes(titles.Length - index).ToString("R"),
                                    SortAddedAt = DateTimeOffset
                                        .UnixEpoch.AddMinutes(titles.Length - index)
                                        .UtcDateTime,
                                }
                        ),
                    ],
                    Response = new TorznabResponseMetadata { Total = titles.Length },
                },
            }
        );
}
