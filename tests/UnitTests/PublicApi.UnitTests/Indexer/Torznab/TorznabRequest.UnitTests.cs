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
        var request = endpointRequest.ToTorznabRequest();

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
        var request = endpointRequest.ToTorznabRequest();

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
        var movie = movieEndpointRequest.ToTorznabRequest();
        var tv = tvEndpointRequest.ToTorznabRequest();
        var mixed = mixedEndpointRequest.ToTorznabRequest();

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
    [Arguments(10_000, 0, true)]
    [Arguments(10_000, 1, false)]
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
}
