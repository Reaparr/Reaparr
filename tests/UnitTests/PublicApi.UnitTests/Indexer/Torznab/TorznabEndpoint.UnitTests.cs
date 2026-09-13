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
    public async Task ShouldRunOnlyRequestedTvSearch_WhenGenericSearchCategoryIsTv()
    {
        // Arrange
        await SetupDatabase(
            6521,
            config =>
            {
                config.RadarrIntegrationCount = 1;
            }
        );

        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var request = new TorznabEndpointRequest
        {
            Type = "search",
            Query = "Silo",
            Categories = [5030],
            Limit = 10,
            Offset = 0,
            ApiKey = "generic-key",
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SearchTvShowCommand>(command =>
                        command.Query == request.Query
                        && command.Limit == request.Offset + request.Limit
                        && command.Offset == 0
                        && command.Integration == integration
                        && command.TorznabApiKey == request.ApiKey
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SearchResult("tv-result"))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integration);

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var responseBody = endpointResult.Endpoint.HttpContext.Response.Body;
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody, leaveOpen: true);
        var xml = await reader.ReadToEndAsync();
        xml.ShouldContain("tv-result");
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<SearchMovieCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldMergeAndLimitResults_WhenGenericSearchHasNoCategories()
    {
        // Arrange
        await SetupDatabase(
            6522,
            config =>
            {
                config.RadarrIntegrationCount = 1;
            }
        );

        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var request = new TorznabEndpointRequest
        {
            Type = "search",
            Query = "Dune",
            Limit = 2,
            Offset = 1,
            ApiKey = "generic-key",
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SearchTvShowCommand>(command =>
                        command.Query == request.Query
                        && command.Limit == request.Offset + request.Limit
                        && command.Offset == 0
                        && command.Integration == integration
                        && command.TorznabApiKey == request.ApiKey
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                SearchResultWithPublicationDates(
                    ("tv-result-1", DateTimeOffset.UnixEpoch.AddMinutes(4)),
                    ("tv-result-2", DateTimeOffset.UnixEpoch.AddMinutes(1)),
                    ("tv-result-3", DateTimeOffset.UnixEpoch.AddMinutes(-2))
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SearchMovieCommand>(command =>
                        command.Query == request.Query
                        && command.Limit == request.Offset + request.Limit
                        && command.Offset == 0
                        && command.Integration == integration
                        && command.TorznabApiKey == request.ApiKey
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                SearchResultWithPublicationDates(
                    ("movie-result-1", DateTimeOffset.UnixEpoch.AddMinutes(3)),
                    ("movie-result-2", DateTimeOffset.UnixEpoch),
                    ("movie-result-3", DateTimeOffset.UnixEpoch.AddMinutes(-3))
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integration);

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var responseBody = endpointResult.Endpoint.HttpContext.Response.Body;
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody, leaveOpen: true);
        var xml = await reader.ReadToEndAsync();
        xml.ShouldContain("movie-result-1");
        xml.ShouldContain("tv-result-2");
        xml.ShouldNotContain("tv-result-1");
        xml.ShouldNotContain("movie-result-2");
        xml.IndexOf("movie-result-1", StringComparison.Ordinal).ShouldBeLessThan(
            xml.IndexOf("tv-result-2", StringComparison.Ordinal)
        );
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnSuccess_WhenOneGenericSearchFailsAfterAnotherReturnsNoItems()
    {
        // Arrange
        await SetupDatabase(
            6523,
            config =>
            {
                config.RadarrIntegrationCount = 1;
            }
        );

        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var request = new TorznabEndpointRequest
        {
            Type = "search",
            Query = "Missing",
            Limit = 10,
            Offset = 0,
            ApiKey = "generic-key",
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SearchTvShowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SearchMovieCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<TorznabMediaSearchResponseDTO>("movie search failed"))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integration);

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var responseBody = endpointResult.Endpoint.HttpContext.Response.Body;
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody, leaveOpen: true);
        var xml = await reader.ReadToEndAsync();
        xml.ShouldContain("<channel");
        xml.ShouldNotContain("movie search failed");
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
    public async Task ShouldReturnTorznabError_WhenRssCommandIsCancelled()
    {
        // Arrange
        await SetupDatabase(6526, config => config.RadarrIntegrationCount = 1);
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var request = new TorznabEndpointRequest { Type = "search", ApiKey = "key" };
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetTorznabRssFeedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultExtensions.TaskIsCancelled(nameof(GetTorznabRssFeedCommand)).ToResult<TorznabMediaSearchResponseDTO>())
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
        xml.ShouldContain("Indexer request cancelled");
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
                        .. titles.Select((title, index) => new TorznabItem
                        {
                            Title = title,
                            PubDate = DateTimeOffset.UnixEpoch.AddMinutes(titles.Length - index).ToString("R"),
                        }),
                    ],
                    Response = new TorznabResponseMetadata { Total = titles.Length },
                },
            }
        );
    private static Result<TorznabMediaSearchResponseDTO> SearchResultWithPublicationDates(
        params (string Title, DateTimeOffset PubDate)[] items
    ) =>
        Result.Ok(
            new TorznabMediaSearchResponseDTO
            {
                Channel = new TorznabChannel
                {
                    Items =
                    [
                        .. items.Select(item => new TorznabItem
                        {
                            Title = item.Title,
                            PubDate = item.PubDate.ToString("R"),
                        }),
                    ],
                    Response = new TorznabResponseMetadata { Total = items.Length },
                },
            }
        );
}
