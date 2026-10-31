using System.Net;
using System.Xml.Linq;

namespace Reaparr.IntegrationTests;

public class TorznabImdbIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.Episode)]
    public async Task ShouldReturnNumericImdbInRssAndSearchWithoutChangingQueryOrStoredIds(PlexMediaType family)
    {
        // Arrange
        var isMovie = family == PlexMediaType.Movie;
        using var container = await CreateContainer(
            new Seed(8637),
            config =>
            {
                config.DatabaseOptions = database =>
                {
                    database.PlexServerCount = 1;
                    database.PlexAccountCount = 1;
                    database.PlexMovieLibraryCount = isMovie ? 1 : 0;
                    database.PlexTvShowLibraryCount = isMovie ? 0 : 1;
                    database.MovieCount = isMovie ? 2 : 0;
                    database.IncludeMultiPartMovies = false;
                    database.TvShowCount = isMovie ? 0 : 2;
                    database.TvShowSeasonCount = isMovie ? 0 : 1;
                    database.TvShowEpisodeCount = isMovie ? 0 : 1;
                    database.RadarrIntegrationCount = isMovie ? 1 : 0;
                    database.SonarrIntegrationCount = isMovie ? 0 : 1;
                };
            }
        );
        var dbContext = container.DbContext;
        if (isMovie)
        {
            var targetId = await dbContext
                .PlexMovies.OrderBy(movie => movie.Id)
                .Select(movie => movie.Id)
                .FirstAsync(CancellationToken);
            await dbContext.PlexMovies.ExecuteUpdateAsync(
                update =>
                    update.SetProperty(
                        movie => movie.Guid_IMDB,
                        movie => movie.Id == targetId ? "tt0000123" : "tt7654321"
                    ),
                CancellationToken
            );
        }
        else
        {
            var targetId = await dbContext
                .PlexTvShows.OrderBy(show => show.Id)
                .Select(show => show.Id)
                .FirstAsync(CancellationToken);
            await dbContext.PlexTvShows.ExecuteUpdateAsync(
                update =>
                    update.SetProperty(show => show.Guid_IMDB, show => show.Id == targetId ? "tt0000123" : "tt7654321"),
                CancellationToken
            );
        }
        var integrationId = isMovie
            ? await dbContext.RadarrIntegrations.Select(integration => integration.Id).SingleAsync(CancellationToken)
            : await dbContext.SonarrIntegrations.Select(integration => integration.Id).SingleAsync(CancellationToken);
        var apiKey = isMovie
            ? await dbContext
                .RadarrIntegrations.Select(integration => integration.TorznabApiKey)
                .SingleAsync(CancellationToken)
            : await dbContext
                .SonarrIntegrations.Select(integration => integration.TorznabApiKey)
                .SingleAsync(CancellationToken);
        var route = $"/api/public/integrations/{integrationId}/indexer/api";
        var category = isMovie ? "2000" : "5000";
        var storedBefore = isMovie
            ? await dbContext
                .PlexMovies.OrderBy(movie => movie.Id)
                .Select(movie => movie.Guid_IMDB)
                .ToListAsync(CancellationToken)
            : await dbContext
                .PlexTvShows.OrderBy(show => show.Id)
                .Select(show => show.Guid_IMDB)
                .ToListAsync(CancellationToken);
        storedBefore.ShouldBe(["tt0000123", "tt7654321"]);
        using var client = container.GetApiClient();

        // Act / Assert — queryless search is the actual RSS path used by Arr setup/polling.
        using var rssResponse = await client.GetAsync(
            $"{route}?t=search&cat={category}&apikey={apiKey}",
            CancellationToken
        );
        rssResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        rssResponse.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
        var rss = XDocument.Parse(await rssResponse.Content.ReadAsStringAsync(CancellationToken));
        var rssItems = rss.Root!.Element("channel")!.Elements("item").ToArray();
        rssItems
            .Select(item =>
                item.Elements()
                    .Single(element => element.Name.LocalName == "attr" && element.Attribute("name")!.Value == "imdb")
                    .Attribute("value")!
                    .Value
            )
            .Order()
            .ShouldBe(["0000123", "7654321"]);
        var targetGuid = rssItems
            .Single(item =>
                item.Elements()
                    .Any(element =>
                        element.Name.LocalName == "attr"
                        && element.Attribute("name")!.Value == "imdb"
                        && element.Attribute("value")!.Value == "0000123"
                    )
            )
            .Element("guid")!
            .Value;
        foreach (var mode in new[] { "search", isMovie ? "movie" : "tvsearch" })
        {
            foreach (var queryId in new[] { "0000123", "tt0000123" })
            {
                using var response = await client.GetAsync(
                    $"{route}?t={mode}&cat={category}&imdbid={queryId}&attrs=imdb&apikey={apiKey}",
                    CancellationToken
                );
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
                var document = XDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
                var items = document.Root!.Element("channel")!.Elements("item").ToArray();
                items.Select(item => item.Element("guid")!.Value).ShouldBe([targetGuid]);
                var attributes = items.Single().Elements().Where(element => element.Name.LocalName == "attr").ToArray();
                attributes
                    .Single(element => element.Attribute("name")!.Value == "imdb")
                    .Attribute("value")!
                    .Value.ShouldBe("0000123");
                attributes
                    .Select(element => element.Attribute("name")!.Value)
                    .Distinct()
                    .Order()
                    .ShouldBe(["category", "imdb", "size"]);
            }
        }
        using var afterContext = container.DbContext;
        var storedAfter = isMovie
            ? await afterContext
                .PlexMovies.OrderBy(movie => movie.Id)
                .Select(movie => movie.Guid_IMDB)
                .ToListAsync(CancellationToken)
            : await afterContext
                .PlexTvShows.OrderBy(show => show.Id)
                .Select(show => show.Guid_IMDB)
                .ToListAsync(CancellationToken);
        storedAfter.ShouldBe(storedBefore);
    }
}
