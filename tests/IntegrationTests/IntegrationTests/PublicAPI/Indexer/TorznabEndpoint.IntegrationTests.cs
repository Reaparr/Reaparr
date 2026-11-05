using System.Net.Http.Headers;
using System.Xml.Linq;

namespace Reaparr.IntegrationTests;

public class TorznabEndpointIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldReturnPagedStableRssXml_WhenRadarrPollsQuerylessSearch()
    {
        // Arrange
        using var container = await CreateContainer(
            new Seed(8663),
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexServerCount = 1;
                    x.PlexAccountCount = 1;
                    x.PlexMovieLibraryCount = 1;
                    x.MovieCount = 4;
                    x.IncludeMultiPartMovies = true;
                    x.RadarrIntegrationCount = 1;
                };
            }
        );
        var integration = await container.DbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var route = $"/api/public/integrations/{integration.Id}/indexer/api";
        var url = $"{route}?t=search&cat=2000&limit=3&offset=1&apikey={integration.TorznabApiKey}";
        var client = container.GetApiClient();

        // Act
        var firstResponse = await client.GetAsync(url, CancellationToken);
        var firstXml = await firstResponse.Content.ReadAsStringAsync(CancellationToken);
        var secondResponse = await client.GetAsync(url, CancellationToken);
        var secondXml = await secondResponse.Content.ReadAsStringAsync(CancellationToken);

        // Assert
        firstResponse.IsSuccessStatusCode.ShouldBeTrue();
        secondResponse.IsSuccessStatusCode.ShouldBeTrue();
        firstResponse.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
        firstXml.ShouldBe(secondXml);

        var document = XDocument.Parse(firstXml);
        var root = document.Root;
        root.ShouldNotBeNull();
        root.Name.LocalName.ShouldBe("rss");
        root.Attribute("version")!.Value.ShouldBe("2.0");
        root.GetNamespaceOfPrefix("torznab")!.NamespaceName.ShouldBe("http://torznab.com/schemas/2015/feed");
        root.GetNamespaceOfPrefix("newznab")!
            .NamespaceName.ShouldBe("http://www.newznab.com/DTD/2010/feeds/attributes/");

        var channel = root.Element("channel");
        channel.ShouldNotBeNull();
        channel.Element("title")!.Value.ShouldBe("Reaparr Indexer");
        channel.Element("description")!.Value.ShouldBe("Reaparr RSS feed");
        channel.Element("language")!.Value.ShouldBe("en-us");
        var response = channel.Elements().Single(x => x.Name.LocalName == "response");
        response.Attribute("offset")!.Value.ShouldBe("1");
        response.Attribute("total")!.Value.ShouldBe("8");

        var items = channel.Elements("item").ToList();
        items.Count.ShouldBe(3);
        var publicationDates = items
            .Select(x => DateTimeOffset.ParseExact(x.Element("pubDate")!.Value, "R", null))
            .ToList();
        publicationDates.ShouldBe(publicationDates.OrderByDescending(x => x));
        foreach (var item in items)
        {
            var guid = item.Element("guid");
            var link = item.Element("link")!.Value;
            var size = long.Parse(item.Element("size")!.Value);
            var enclosure = item.Element("enclosure");
            var attributes = item.Elements().Where(x => x.Name.LocalName == "attr").ToList();

            guid.ShouldNotBeNull();
            guid.Attribute("isPermaLink")!.Value.ShouldBe("false");
            guid.Value.ShouldStartWith("reaparr-");
            guid.Value.ShouldNotBe(link);
            attributes
                .Single(x => x.Attribute("name")!.Value == "size")
                .Attribute("value")!
                .Value.ShouldBe(size.ToString());
            var categories = attributes.Where(x => x.Attribute("name")!.Value == "category").ToList();
            categories.Count.ShouldBeGreaterThanOrEqualTo(2);
            categories.All(x => int.TryParse(x.Attribute("value")!.Value, out _)).ShouldBeTrue();
            enclosure.ShouldNotBeNull();
            enclosure.Attribute("url")!.Value.ShouldBe(link);
            enclosure.Attribute("length")!.Value.ShouldBe(size.ToString());
            enclosure.Attribute("type")!.Value.ShouldBe("application/x-bittorrent");
            link.ShouldContain("apikey=");
        }
    }

    [Test]
    public async Task ShouldReturnExactPagedRssTotal_WhenSonarrPollsQuerylessTvSearch()
    {
        // Arrange
        using var container = await CreateContainer(
            new Seed(8675),
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexServerCount = 1;
                    x.PlexAccountCount = 1;
                    x.PlexTvShowLibraryCount = 1;
                    x.TvShowCount = 1;
                    x.TvShowSeasonCount = 1;
                    x.TvShowEpisodeCount = 5;
                    x.SonarrIntegrationCount = 1;
                };
            }
        );
        var integration = await container.DbContext.SonarrIntegrations.SingleAsync(CancellationToken);
        var route = $"/api/public/integrations/{integration.Id}/indexer/api";
        var url = $"{route}?t=tvsearch&cat=5000&limit=2&offset=1&apikey={integration.TorznabApiKey}";
        var client = container.GetApiClient();

        // Act
        var response = await client.GetAsync(url, CancellationToken);
        var xml = await response.Content.ReadAsStringAsync(CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
        var channel = XDocument.Parse(xml).Root!.Element("channel")!;
        var metadata = channel.Elements().Single(x => x.Name.LocalName == "response");
        metadata.Attribute("offset")!.Value.ShouldBe("1");
        metadata.Attribute("total")!.Value.ShouldBe("5");
        channel.Elements("item").Count().ShouldBe(2);
    }

    [Test]
    [Arguments("season=00&ep=1", true, "0:1")]
    [Arguments("season=00", false, "0:1,0:2")]
    [Arguments("season=00&ep=0", false, "0:1,0:2")]
    [Arguments("ep=1", true, "0:1,1:1,2:1")]
    [Arguments("season=1&ep=1", true, "1:1")]
    [Arguments("", false, "0:1,0:2,1:1,1:2,2:1,2:2")]
    public async Task ShouldPreserveSeasonSelection_WhenSonarrSearchesEpisodes(
        string selectors,
        bool includeShowId,
        string expectedEpisodeSelectors
    )
    {
        // Arrange
        using var container = await CreateContainer(
            new Seed(8676),
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexServerCount = 1;
                    x.PlexAccountCount = 1;
                    x.PlexTvShowLibraryCount = 1;
                    x.TvShowCount = 1;
                    x.TvShowSeasonCount = 3;
                    x.TvShowEpisodeCount = 2;
                    x.SonarrIntegrationCount = 1;
                };
            }
        );
        await container.DbContext.PlexTvShowSeason.ExecuteUpdateAsync(
            x => x.SetProperty(season => season.SeasonNumber, season => season.SeasonNumber - 1),
            CancellationToken
        );
        var integration = await container.DbContext.SonarrIntegrations.SingleAsync(CancellationToken);
        var show = await container.DbContext.PlexTvShows.SingleAsync(CancellationToken);
        var showSelector = includeShowId ? $"&tvdbid={show.Guid_TVDB!.Value}" : string.Empty;
        var route = $"/api/public/integrations/{integration.Id}/indexer/api";
        var url = $"{route}?t=tvsearch&cat=5000&limit=50&apikey={integration.TorznabApiKey}&{selectors}{showSelector}";
        var expected = expectedEpisodeSelectors.Split(',');
        var client = container.GetApiClient();

        // Act
        var response = await client.GetAsync(url, CancellationToken);
        var xml = await response.Content.ReadAsStringAsync(CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
        var channel = XDocument.Parse(xml).Root!.Element("channel")!;
        channel
            .Elements("item")
            .Select(item =>
            {
                var attributes = item.Elements().Where(x => x.Name.LocalName == "attr");
                var season = attributes.Single(x => x.Attribute("name")!.Value == "season").Attribute("value")!.Value;
                var episode = attributes.Single(x => x.Attribute("name")!.Value == "episode").Attribute("value")!.Value;
                return $"{season}:{episode}";
            })
            .Order()
            .ShouldBe(expected);
        channel
            .Elements()
            .Single(x => x.Name.LocalName == "response")
            .Attribute("total")!.Value.ShouldBe(expected.Length.ToString());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldSearchDailyEpisodesByPersistedAirDate(bool searchByTitle)
    {
        // Arrange
        using var container = await CreateContainer(
            new Seed(8677),
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexServerCount = 1;
                    x.PlexAccountCount = 1;
                    x.PlexTvShowLibraryCount = 1;
                    x.TvShowCount = 2;
                    x.TvShowSeasonCount = 1;
                    x.TvShowEpisodeCount = 3;
                    x.SonarrIntegrationCount = 1;
                };
            }
        );
        await container.DbContext.PlexTvShowEpisodes.ExecuteUpdateAsync(
            x => x.SetProperty(episode => episode.OriginallyAvailableAt, (DateTime?)null),
            CancellationToken
        );
        await container.DbContext.PlexTvShowEpisodes.Where(x => x.EpisodeNumber == 1).ExecuteUpdateAsync(
            x => x.SetProperty(episode => episode.OriginallyAvailableAt, new DateTime(2024, 2, 29, 19, 30, 0, DateTimeKind.Utc)),
            CancellationToken
        );
        await container.DbContext.PlexTvShowEpisodes.Where(x => x.EpisodeNumber == 2).ExecuteUpdateAsync(
            x => x.SetProperty(episode => episode.OriginallyAvailableAt, new DateTime(2024, 2, 28, 0, 0, 0, DateTimeKind.Utc)),
            CancellationToken
        );
        var integration = await container.DbContext.SonarrIntegrations.SingleAsync(CancellationToken);
        var show = await container.DbContext.PlexTvShows.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var targetEpisode = await container.DbContext.PlexTvShowEpisodes.SingleAsync(
            x => x.TvShowId == show.Id && x.EpisodeNumber == 1,
            CancellationToken
        );
        var showSelector = searchByTitle ? $"q={Uri.EscapeDataString(show.Title)}" : $"tvdbid={show.Guid_TVDB!.Value}";
        var route = $"/api/public/integrations/{integration.Id}/indexer/api";
        var url = $"{route}?t=tvsearch&cat=5000&apikey={integration.TorznabApiKey}&{showSelector}";
        var client = container.GetApiClient();

        // Act
        var response = await client.GetAsync($"{url}&season=2024&ep=02%2F29", CancellationToken);
        var xml = await response.Content.ReadAsStringAsync(CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
        var channel = XDocument.Parse(xml).Root!.Element("channel")!;
        channel.Elements("item").ShouldHaveSingleItem().Element("link")!.Value.ShouldContain($"MediaId={targetEpisode.Id}&");
        channel.Elements().Single(x => x.Name.LocalName == "response").Attribute("total")!.Value.ShouldBe("1");
        (await GetRssItems(client, $"{url}&season=2023&ep=02%2F28")).ShouldBeEmpty();

        // Missing air-date metadata must not fall back to an unfiltered or numeric-season search.
        await container.DbContext.PlexTvShowEpisodes.Where(x => x.Id == targetEpisode.Id).ExecuteUpdateAsync(
            x => x.SetProperty(episode => episode.OriginallyAvailableAt, (DateTime?)null),
            CancellationToken
        );
        (await GetRssItems(client, $"{url}&season=2024&ep=02%2F29")).ShouldBeEmpty();
    }

    [Test]
    [Arguments("season=2023&ep=02%2F29")]
    [Arguments("ep=02%2F29")]
    [Arguments("season=2024&ep=banana")]
    public async Task ShouldReturnTorznabErrorXml_WhenDailyEpisodeSelectorIsInvalid(string selector) =>
        await AssertInvalidRequest($"t=tvsearch&{selector}", 8678);

    [Test]
    public async Task ShouldRemoveMovieFromRss_WhenSynchronizationDeletesIt()
    {
        // Arrange
        using var container = await CreateContainer(
            new Seed(8669),
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexServerCount = 1;
                    x.PlexAccountCount = 1;
                    x.PlexMovieLibraryCount = 1;
                    x.MovieCount = 2;
                    x.RadarrIntegrationCount = 1;
                };
            }
        );
        var integration = await container.DbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var route = $"/api/public/integrations/{integration.Id}/indexer/api";
        var url = $"{route}?t=search&cat=2000&limit=50&offset=0&apikey={integration.TorznabApiKey}";
        var client = container.GetApiClient();
        (await GetRssItems(client, url)).Count.ShouldBeGreaterThan(0);
        var library = await container.DbContext.PlexLibraries.SingleAsync(CancellationToken);

        // Act
        var syncResult = await container
            .Resolve<ICommandExecutor>()
            .Send(new SyncPlexMoviesCommand(new InsertMediaMetaDataCommandResponse(library)), CancellationToken);

        // Assert
        syncResult.IsSuccess.ShouldBeTrue();
        syncResult.Value.DeletedMovies.ShouldBe(2);
        (await GetRssItems(client, url)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldCreateIntegrationOwnedDownloadTask_WhenRssTorrentIsSubmitted()
    {
        // Arrange
        using var container = await CreateContainer(
            new Seed(8674),
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexServerCount = 1;
                    x.PlexAccountCount = 1;
                    x.PlexMovieLibraryCount = 1;
                    x.MovieCount = 1;
                    x.RadarrIntegrationCount = 1;
                };
            }
        );
        var integration = await container.DbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var route = $"/api/public/integrations/{integration.Id}";
        var rssUrl = $"{route}/indexer/api?t=search&cat=2000&limit=1&offset=0&apikey={integration.TorznabApiKey}";
        var client = container.GetApiClient();

        // Act
        var rssResponse = await client.GetAsync(rssUrl, CancellationToken);
        var rssXml = await rssResponse.Content.ReadAsStringAsync(CancellationToken);
        var item = XDocument.Parse(rssXml).Root!.Element("channel")!.Elements("item").Single();
        var torrentResponse = await client.GetAsync(item.Element("link")!.Value, CancellationToken);
        var torrentBytes = await torrentResponse.Content.ReadAsByteArrayAsync(CancellationToken);
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(torrentBytes), "torrents", "rss-release.torrent");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            integration.QBittorrentApiKey
        );
        var addResponse = await client.PostAsync(
            $"{route}/download-client/api/v2/torrents/add",
            upload,
            CancellationToken
        );

        // Assert
        rssResponse.IsSuccessStatusCode.ShouldBeTrue();
        torrentResponse.IsSuccessStatusCode.ShouldBeTrue();
        torrentResponse.Content.Headers.ContentType!.MediaType.ShouldBe("application/x-bittorrent");
        addResponse.IsSuccessStatusCode.ShouldBeTrue();
        (await addResponse.Content.ReadAsStringAsync(CancellationToken)).ShouldBe("Ok.");
        var task = await container.DbContext.DownloadTaskMovie.Include(x => x.Children).SingleAsync(CancellationToken);
        task.RadarrIntegrationId.ShouldBe(integration.Id);
        task.SonarrIntegrationId.ShouldBeNull();
        task.Children.ShouldHaveSingleItem();
    }

    [Test]
    public async Task ShouldReturnTorznabErrorXml_WhenOffsetIsNegative() =>
        await AssertInvalidRequest("offset=-1", 8664);

    [Test]
    public async Task ShouldReturnTorznabErrorXml_WhenLimitIsNegative() => await AssertInvalidRequest("limit=-1", 8665);

    [Test]
    public async Task ShouldReturnTorznabErrorXml_WhenTypeIsUnknown() => await AssertInvalidRequest("t=book", 8666);

    [Test]
    public async Task ShouldReturnTorznabErrorXml_WhenExtendedValueIsInvalid() =>
        await AssertInvalidRequest("extended=2", 8667);

    [Test]
    public async Task ShouldReturnTorznabErrorXml_WhenAttributesAreMalformed() =>
        await AssertInvalidRequest("attrs=size%2Ccategory%21", 8668);

    [Test]
    public async Task ShouldReturnTorznabErrorXml_WhenSeasonIsNegative() =>
        await AssertInvalidRequest("t=tvsearch&season=-1", 8670);

    [Test]
    public async Task ShouldReturnTorznabErrorXml_WhenEpisodeIsNegative() =>
        await AssertInvalidRequest("t=tvsearch&ep=-1", 8671);

    [Test]
    public async Task ShouldReturnTorznabErrorXml_WhenTvdbIdIsNegative() =>
        await AssertInvalidRequest("t=tvsearch&tvdbid=-1", 8672);

    [Test]
    public async Task ShouldReturnTorznabErrorXml_WhenTmdbIdIsNegative() =>
        await AssertInvalidRequest("t=movie&tmdbid=-1", 8673);

    private async Task<List<XElement>> GetRssItems(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, CancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue();
        var xml = await response.Content.ReadAsStringAsync(CancellationToken);
        return XDocument.Parse(xml).Root!.Element("channel")!.Elements("item").ToList();
    }

    private async Task AssertInvalidRequest(string invalidQuery, int seedValue)
    {
        // Arrange
        using var container = await CreateContainer(
            new Seed(seedValue),
            config => config.DatabaseOptions = x => x.RadarrIntegrationCount = 1
        );
        var integration = await container.DbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var route = $"/api/public/integrations/{integration.Id}/indexer/api";
        var separator = invalidQuery.StartsWith("t=", StringComparison.Ordinal) ? string.Empty : "t=search&";
        var url = $"{route}?{separator}{invalidQuery}&apikey={integration.TorznabApiKey}";
        var client = container.GetApiClient();

        // Act
        var response = await client.GetAsync(url, CancellationToken);
        var xml = await response.Content.ReadAsStringAsync(CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
        var error = XDocument.Parse(xml).Root;
        error.ShouldNotBeNull();
        error.Name.LocalName.ShouldBe("error");
        error.Attribute("code")!.Value.ShouldBe("201");
        error.Attribute("description")!.Value.ShouldBe("Incorrect parameter");
    }
}
