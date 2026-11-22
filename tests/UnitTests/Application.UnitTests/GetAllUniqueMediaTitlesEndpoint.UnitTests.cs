namespace Reaparr.Application.UnitTests;

public class GetAllUniqueMediaTitlesEndpointUnitTests
    : BaseEndpointUnitTest<GetAllUniqueMediaTitlesEndpoint, GetAllUniqueMediaTitlesEndpointRequest, List<string>>
{
    [Test]
    public async Task ShouldReturnOnlyTitlesMatchingSupportedCharacters_WhenListingMovieTitles()
    {
        // Arrange
        await SetupDatabase(147321, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 3;
        });

        var dbContext = IDbContext;
        var movies = await dbContext.PlexMovies.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        movies.Count.ShouldBe(3);
        movies[0].Title = "Alpha: One";
        movies[1].Title = "Beta-2";
        movies[2].Title = "Björk!";
        await dbContext.SaveChangesAsync(CancellationToken);

        (await IDbContext.PlexMovies.Select(x => x.Title).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldContain(
            "Alpha: One"
        );
        (await IDbContext.PlexMovies.Select(x => x.Title).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldContain(
            "Beta-2"
        );
        (await IDbContext.PlexMovies.Select(x => x.Title).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldContain(
            "Björk!"
        );

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetAllUniqueMediaTitlesEndpointRequest { Type = PlexMediaType.Movie }
        );

        endpointResult.IsValid.ShouldBeTrue();
        var responseBody = endpointResult.Endpoint.HttpContext.Response.Body;
        responseBody.Position = 0;
        using var responseReader = new StreamReader(responseBody, leaveOpen: true);
        var titles = JsonSerializer.Deserialize<List<string>>(
            await responseReader.ReadToEndAsync(CancellationToken),
            DefaultJsonSerializerOptions.ConfigStandard
        );
        titles.ShouldBe(["Alpha: One", "Beta-2"]);
    }
}
