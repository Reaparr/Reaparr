using System.Xml.Linq;

namespace Reaparr.IntegrationTests;

public class RefreshLibraryMediaEndpointIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldFullyRefreshLibraryMedia_WhenPlexLibraryIsOfTypeMovieAndCommandIsSent()
    {
        // Arrange
        var seed = new Seed(8932);
        const int serverCount = 1;
        const int libraryCount = 1;
        const int movieCount = 50;
        const int roleCount = 2;
        const int genreCount = 1;
        const int countryCount = 1;
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (x, _) =>
                {
                    x.SetupIdentityRequest(seed);
                };

                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexMovieLibraryCount = 1;
                    x.MovieCount = 0;
                    x.RadarrIntegrationCount = 1;
                };

                config.BaseMockHttpClientOptions = x =>
                {
                    x.GenerateFromDatabase = true;
                    x.PlexServerAccessCount = serverCount;
                    x.MovieLibraryCount = libraryCount;
                    x.MoviesPerLibraryCount = movieCount;
                    x.RolePerMediaItemCount = roleCount;
                    x.CountriesPerMediaItemCount = countryCount;
                    x.GenrePerMediaItemCount = genreCount;
                };
            }
        );

        var plexLibrary = await container.DbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();
        var radarrIntegration = await container.DbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var rssUrl = $"/api/public/integrations/{radarrIntegration.Id}/indexer/api?t=search&cat=2000&limit=100&offset=0&apikey={radarrIntegration.TorznabApiKey}";

        // Act
        var client = container.GetApiClient();
        await client.SignIn();
        (await GetRssItemCount(client, rssUrl)).ShouldBe(0);

        var testResult = await client.POSTAsync<
            RefreshLibraryMediaEndpoint,
            RefreshLibraryMediaEndpointRequest,
            ResultDTO<PlexLibraryDTO>
        >(new RefreshLibraryMediaEndpointRequest() { PlexLibraryId = plexLibrary.Id });

        // Assert
        var result = testResult.Result;
        result.IsSuccess.ShouldBeTrue();
        testResult.Response.IsSuccessStatusCode.ShouldBeTrue();

        // Wait for the scheduler to finish the library sync job
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);

        // Verify the library was refreshed
        var dbContext = container.DbContext;
        var refreshedLibrary = await dbContext
            .PlexLibraries.Include(x => x.Movies)
            .FirstOrDefaultAsync(x => x.Id == plexLibrary.Id, CancellationToken);

        refreshedLibrary.ShouldNotBeNull();
        refreshedLibrary.Movies.Count.ShouldBe(movieCount);
        refreshedLibrary.SyncedAt.ShouldNotBeNull();
        refreshedLibrary.ActorsCount.ShouldBeGreaterThan(0);
        refreshedLibrary.ActorsCount.ShouldBeLessThanOrEqualTo(roleCount * movieCount);
        refreshedLibrary.GenresCount.ShouldBeGreaterThan(0);
        refreshedLibrary.GenresCount.ShouldBeLessThanOrEqualTo(genreCount * movieCount);
        refreshedLibrary.CountriesCount.ShouldBeGreaterThan(0);
        refreshedLibrary.CountriesCount.ShouldBeLessThanOrEqualTo(countryCount * movieCount);

        var movieActorCount = await dbContext.PlexMovieActors.CountAsync(CancellationToken);
        movieActorCount.ShouldBe(roleCount * movieCount);

        var genreMovieCount = await dbContext.PlexMovieGenres.CountAsync(CancellationToken);
        genreMovieCount.ShouldBe(genreCount * movieCount);

        var countryMovieCount = await dbContext.PlexMovieCountries.CountAsync(CancellationToken);
        countryMovieCount.ShouldBe(countryCount * movieCount);

        // Verify media data
        var movies = await dbContext.PlexMovies.IncludeMediaData().ToListAsync(CancellationToken);
        movies.ShouldNotBeEmpty();
        movies.Count.ShouldBe(movieCount);

        var mediaList = movies.SelectMany(x => x.MediaDataList).ToList();
        mediaList.Count.ShouldBeGreaterThanOrEqualTo(movieCount);
        (await GetRssItemCount(client, rssUrl)).ShouldBe(mediaList.Count);
    }

    [Test]
    public async Task ShouldFullyRefreshLibraryMedia_WhenPlexLibraryIsOfTypeTvShowAndCommandIsSent()
    {
        // Arrange
        var seed = new Seed(8932);
        const int serverCount = 1;
        const int libraryCount = 1;
        const int tvShowCount = 20;
        const int seasonCount = 2;
        const int episodeCount = 4;
        const int roleCount = 2;
        const int genreCount = 1;
        const int countryCount = 1;
        const int totalEpisodeCount = tvShowCount * seasonCount * episodeCount;

        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (x, _) => x.SetupIdentityRequest(seed);

                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexTvShowLibraryCount = 1;
                    x.TvShowCount = 0;
                    x.SonarrIntegrationCount = 1;
                };

                config.BaseMockHttpClientOptions = x =>
                {
                    x.GenerateFromDatabase = true;
                    x.PlexServerAccessCount = serverCount;
                    x.TvShowLibraryCount = libraryCount;
                    x.TvShowsPerLibraryCount = tvShowCount;
                    x.SeasonsPerTvShowCount = seasonCount;
                    x.EpisodesPerSeasonCount = episodeCount;
                    x.RolePerMediaItemCount = roleCount;
                    x.CountriesPerMediaItemCount = countryCount;
                    x.GenrePerMediaItemCount = genreCount;
                };
            }
        );

        var plexLibrary = await container.DbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();
        var sonarrIntegration = await container.DbContext.SonarrIntegrations.SingleAsync(CancellationToken);
        var rssUrl = $"/api/public/integrations/{sonarrIntegration.Id}/indexer/api?t=tvsearch&cat=5000&limit=1000&offset=0&apikey={sonarrIntegration.TorznabApiKey}";

        // Act
        var client = container.GetApiClient();
        await client.SignIn();
        (await GetRssItemCount(client, rssUrl)).ShouldBe(0);

        var testResult = await client.POSTAsync<
            RefreshLibraryMediaEndpoint,
            RefreshLibraryMediaEndpointRequest,
            ResultDTO<PlexLibraryDTO>
        >(new RefreshLibraryMediaEndpointRequest { PlexLibraryId = plexLibrary.Id });

        // Assert
        var result = testResult.Result;
        result.IsSuccess.ShouldBeTrue();
        testResult.Response.IsSuccessStatusCode.ShouldBeTrue();

        // Wait for the scheduler to finish the library sync job
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);

        // Verify the library was refreshed
        var dbContext = container.DbContext;
        var refreshedLibrary = await dbContext
            .PlexLibraries.Include(x => x.TvShows)
                .ThenInclude(x => x.Seasons)
                    .ThenInclude(x => x.Episodes)
            .FirstOrDefaultAsync(x => x.Id == plexLibrary.Id, CancellationToken);

        refreshedLibrary.ShouldNotBeNull();
        refreshedLibrary.TvShows.Count.ShouldBe(tvShowCount);
        refreshedLibrary.TvShows.Sum(x => x.Seasons.Count).ShouldBe(tvShowCount * seasonCount);
        refreshedLibrary.TvShows.Sum(x => x.Seasons.Sum(s => s.Episodes.Count)).ShouldBe(totalEpisodeCount);
        refreshedLibrary.SyncedAt.ShouldNotBeNull();
        refreshedLibrary.ActorsCount.ShouldBeGreaterThan(0);
        refreshedLibrary.GenresCount.ShouldBeGreaterThan(0);
        refreshedLibrary.CountriesCount.ShouldBeGreaterThan(0);

        var actors = await dbContext.PlexTvShowActors.ToListAsync(CancellationToken);
        actors.ShouldNotBeEmpty();

        var genres = await dbContext.PlexTvShowGenres.ToListAsync(CancellationToken);
        genres.ShouldNotBeEmpty();

        var countries = await dbContext.PlexTvShowCountries.ToListAsync(CancellationToken);
        countries.ShouldNotBeEmpty();

        // Verify the library was refreshed
        var movieActorCount = await dbContext.PlexTvShowActors.CountAsync(CancellationToken);
        movieActorCount.ShouldBe(roleCount * tvShowCount);

        var genreTvShowCount = await dbContext.PlexTvShowGenres.CountAsync(CancellationToken);
        genreTvShowCount.ShouldBe(genreCount * tvShowCount);

        var countryTvShowCount = await dbContext.PlexTvShowCountries.CountAsync(CancellationToken);
        countryTvShowCount.ShouldBe(countryCount * tvShowCount);

        // Verify media data
        var episodes = await dbContext.PlexTvShowEpisodes.IncludeMediaData().ToListAsync(CancellationToken);
        episodes.ShouldNotBeEmpty();
        episodes.Count.ShouldBe(totalEpisodeCount);

        var mediaList = episodes.SelectMany(x => x.MediaDataList).ToList();
        mediaList.Count.ShouldBeGreaterThanOrEqualTo(totalEpisodeCount);
        (await GetRssItemCount(client, rssUrl)).ShouldBe(mediaList.Count);
    }
    private async Task<int> GetRssItemCount(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, CancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue();
        var xml = await response.Content.ReadAsStringAsync(CancellationToken);
        return XDocument.Parse(xml).Root!.Element("channel")!.Elements("item").Count();
    }
}
