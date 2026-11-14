using Reaparr.Application.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class TorznabRequestUnitTests
{
    [Test]
    public void ShouldMapEndpointValuesAndDefaults_WhenCreatingTypedRequest()
    {
        // Arrange
        var endpointRequest = new TorznabEndpointRequest
        {
            Type = "tvsearch",
            Query = "Silo",
            Season = 2,
            Episode = 3,
            TvdbId = 123,
            ApiKey = "key",
            Categories = [5030],
        };

        // Act
        var request = endpointRequest.ToTorznabRequest(new IntegrationIdentity(IntegrationType.Sonarr, Guid.Empty));

        // Assert
        request.Type.ShouldBe(TorznabQueryType.TvSearch);
        request.Query.ShouldBe("Silo");
        request.Season.ShouldBe(2);
        request.Episode.ShouldBe(3);
        request.TvdbId.ShouldBe(123);
        request.Limit.ShouldBe(50);
        request.Offset.ShouldBe(0);
        request.Categories.ShouldBe([5030]);
        request.Mode.ShouldBe(TorznabRequestMode.ActiveSearch);
    }

    [Test]
    [Arguments("caps", TorznabQueryType.Caps)]
    [Arguments("search", TorznabQueryType.Search)]
    [Arguments("tvsearch", TorznabQueryType.TvSearch)]
    [Arguments("movie", TorznabQueryType.Movie)]
    [Arguments("TVSEARCH", TorznabQueryType.TvSearch)]
    public void ShouldParseSupportedTypeWithoutCaseSensitivity(string value, TorznabQueryType expected)
    {
        // Arrange
        var endpointRequest = new TorznabEndpointRequest { Type = value, ApiKey = "key" };

        // Act
        var request = endpointRequest.ToTorznabRequest(new IntegrationIdentity(IntegrationType.Sonarr, Guid.Empty));

        // Assert
        request.Type.ShouldBe(expected);
        endpointRequest.ParsedType.ShouldBe(expected);
    }

    [Test]
    public void ShouldRejectUnsupportedType()
    {
        // Arrange
        var endpointRequest = new TorznabEndpointRequest { Type = "book", ApiKey = "key" };

        // Assert
        endpointRequest.ParsedType.ShouldBe(TorznabQueryType.Unknown);
    }

    [Test]
    [Arguments("0")]
    [Arguments("1")]
    [Arguments("-1")]
    [Arguments("")]
    [Arguments(" ")]
    public void ShouldRejectNumericAndBlankQueryTypes(string value)
    {
        // Arrange
        var endpointRequest = new TorznabEndpointRequest { Type = value, ApiKey = "key" };

        // Act
        var result = endpointRequest.ParsedType;

        // Assert
        result.ShouldBe(TorznabQueryType.Unknown);
    }

    [Test]
    public void ShouldDeriveRssMediaTypesFromQueryTypeAndCategories()
    {
        // Arrange
        var movieEndpointRequest = new TorznabEndpointRequest { Type = "movie", ApiKey = "key" };
        var tvEndpointRequest = new TorznabEndpointRequest { Type = "tvsearch", ApiKey = "key" };
        var mixedEndpointRequest = new TorznabEndpointRequest { Type = "search", ApiKey = "key" };

        // Act
        var movie = movieEndpointRequest.ToTorznabRequest(new IntegrationIdentity(IntegrationType.Radarr, Guid.Empty));
        var tv = tvEndpointRequest.ToTorznabRequest(new IntegrationIdentity(IntegrationType.Sonarr, Guid.Empty));
        var mixed = mixedEndpointRequest.ToTorznabRequest(new IntegrationIdentity(IntegrationType.Sonarr, Guid.Empty));

        // Assert
        movie.Mode.ShouldBe(TorznabRequestMode.Rss);
        movie.IncludesMovies.ShouldBeTrue();
        movie.IncludesEpisodes.ShouldBeFalse();
        tv.IncludesMovies.ShouldBeFalse();
        tv.IncludesEpisodes.ShouldBeTrue();
        mixed.IncludesMovies.ShouldBeTrue();
        mixed.IncludesEpisodes.ShouldBeTrue();
    }

    [Test]
    [Arguments(100, 9_900, true)]
    [Arguments(100, 9_901, false)]
    [Arguments(101, 0, false)]
    [Arguments(int.MaxValue, int.MaxValue, false)]
    public void ShouldValidatePaginationWindowWithoutIntegerOverflow(int limit, int offset, bool expectedValid)
    {
        // Arrange
        var endpointRequest = new TorznabEndpointRequest
        {
            Type = "search",
            ApiKey = "key",
            Limit = limit,
            Offset = offset,
        };
        var validator = new TorznabEndpointRequestValidator();

        // Act
        var result = validator.Validate(endpointRequest);

        // Assert
        result.IsValid.ShouldBe(expectedValid);
    }

    [Test]
    [Arguments(TorznabQueryType.Search, ExternalIndexerQueryType.Search)]
    [Arguments(TorznabQueryType.TvSearch, ExternalIndexerQueryType.TvSearch)]
    [Arguments(TorznabQueryType.Movie, ExternalIndexerQueryType.Movie)]
    public void ShouldProjectSearchAndRssWithoutCredentials_WhenBuildingTelemetryReports(
        TorznabQueryType queryType,
        ExternalIndexerQueryType expectedType
    )
    {
        // Arrange
        var integration = new IntegrationIdentity(
            IntegrationType.Radarr,
            Guid.Parse("473cf359-8c22-4f5d-8641-2a6384b48c65")
        );
        var rssRequest = new TorznabRequest
        {
            Type = queryType,
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            TvdbId = 0,
            ImdbId = string.Empty,
            TmdbId = 0,
            ApiKey = "private-indexer-key",
            Limit = 50,
            Offset = 0,
            Categories = [2000, 5030],
            Attributes = [],
            Integration = integration,
        };
        var searchRequest = rssRequest with
        {
            Query = "The Matrix",
            ImdbId = "tt0133093",
            TmdbId = 603,
            TvdbId = 456,
            Season = 2,
            Episode = 3,
        };
        var response = Result.Ok(new TorznabMediaSearchResponseDTO
        {
            Channel = new TorznabChannel
            {
                Items = [new TorznabItem(), new TorznabItem()],
                Response = new TorznabResponseMetadata { Total = 7 },
            },
        });
        var cancelled = Result.Fail<TorznabMediaSearchResponseDTO>(
            new ExceptionalError(new OperationCanceledException())
        );

        // Act
        var rss = rssRequest.ToTelemetryReport(response);
        var search = searchRequest.ToTelemetryReport(cancelled);

        // Assert
        rss.Integration.ShouldBe(integration);
        rss.Type.ShouldBe(expectedType);
        rss.Mode.ShouldBe(TelemetryMode.Rss);
        rss.Query.ShouldBe(string.Empty);
        rss.ImdbId.ShouldBe(string.Empty);
        rss.TmdbId.ShouldBe(0);
        rss.TvdbId.ShouldBe(0);
        rss.Season.ShouldBe(0);
        rss.Episode.ShouldBe(0);
        rss.Categories.ShouldBe([2000, 5030]);
        rss.ReturnedItemCount.ShouldBe(2);
        rss.TotalCount.ShouldBe(7);
        search.Integration.ShouldBe(integration);
        search.Type.ShouldBe(expectedType);
        search.Mode.ShouldBe(TelemetryMode.Search);
        search.Query.ShouldBe("The Matrix");
        search.ImdbId.ShouldBe("tt0133093");
        search.TmdbId.ShouldBe(603);
        search.TvdbId.ShouldBe(456);
        search.Season.ShouldBe(2);
        search.Episode.ShouldBe(3);
        search.Categories.ShouldBe([2000, 5030]);
        search.ReturnedItemCount.ShouldBe(0);
        search.TotalCount.ShouldBe(0);
        System.Text.Json.JsonSerializer.Serialize(new[] { rss, search }).ShouldNotContain(rssRequest.ApiKey);
    }
}
