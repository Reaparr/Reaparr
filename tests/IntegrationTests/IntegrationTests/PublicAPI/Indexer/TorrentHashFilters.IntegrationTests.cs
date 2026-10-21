using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Reaparr.IntegrationTests;

public class TorrentHashFiltersIntegrationTests : BaseIntegrationTests
{
    private const string Category = "Custom Mixed Radarr";
    private const string MovieHash = "0123AbCd4567Ef890123AbCd4567Ef890123AbCd";
    private const string EpisodeHash = "abcdefabcdefabcdefabcdefabcdefabcdefabcd";
    private const string ForeignMovieHash = "89abcdef0123456789abcdef0123456789abcdef";
    private const string ForeignEpisodeHash = "1234567812345678123456781234567812345678";

    [Test]
    public async Task ShouldFilterAuthenticatedTorrentHashesWithoutChangingWireFieldsOrTasks()
    {
        // Arrange
        using var container = await CreateContainer(new Seed(8641), config => config.DatabaseOptions = database =>
        {
            database.PlexServerCount = 1;
            database.MovieDownloadTasksCount = 2;
            database.TvShowDownloadTasksCount = 2;
            database.TvShowSeasonDownloadTasksCount = 1;
            database.TvShowEpisodeDownloadTasksCount = 1;
            database.RadarrIntegrationCount = 2;
        });
        using var db = container.DbContext;
        var integrations = await db.RadarrIntegrations.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        integrations.Select(x => x.Id).Distinct().Count().ShouldBe(2);
        integrations[0].Category = Category;
        integrations[1].Category = "Foreign Radarr";
        var owners = integrations.Select(x => x.Id).ToArray();
        var files = await ArrangeMovieFilesAsync(db, owners);
        files.AddRange(await ArrangeEpisodeFilesAsync(db, owners));
        await db.PlexServers.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true)
            .SetProperty(y => y.OwnedOverride, false), CancellationToken);
        await db.SaveChangesAsync(CancellationToken);
        var before = files.OrderBy(x => x.Id).Select(x =>
            (x.Id, ParentId: x.ToParentKey()!.Id, x.HashId, x.DownloadStatus, x.SonarrIntegrationId, x.RadarrIntegrationId)).ToArray();
        var movie = ExpectedWire(files[0], MovieHash, "pausedUP");
        var episode = ExpectedWire(files[2], EpisodeHash, "downloading");
        var both = new[] { movie, episode }.OrderBy(x => x.Hash).ToArray();
        (string Query, TorrentWire[] Expected)[] requests =
        [
            ($"hashes={MovieHash.ToUpperInvariant()}", [movie]),
            ($"hashes={Uri.EscapeDataString($"{MovieHash.ToUpperInvariant()}|{EpisodeHash.ToUpperInvariant()}|{ForeignMovieHash}|{ForeignEpisodeHash}")}", both),
            ("hashes=ALL", both),
            ($"hashes=all&category={Uri.EscapeDataString(Category.ToLowerInvariant())}", both),
            ("hashes=deadbeefdeadbeefdeadbeefdeadbeefdeadbeef", []),
        ];
        using var client = container.GetApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", integrations[0].QBittorrentApiKey);
        var route = $"/api/public/integrations/{integrations[0].Id}/download-client/api/v2/torrents/info";

        // Act and Assert — each request crosses real routing, authentication, SQL and JSON serialization.
        foreach (var request in requests)
        {
            using var response = await client.GetAsync($"{route}?{request.Query}", CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
            var actual = document.RootElement.EnumerateArray().Select(x => new TorrentWire(
                x.GetProperty("hash").GetString(), x.GetProperty("state").GetString(), x.GetProperty("category").GetString(),
                x.GetProperty("save_path").GetString(), x.GetProperty("content_path").GetString(),
                x.GetProperty("name").GetString(), x.GetProperty("size").GetInt64())).OrderBy(x => x.Hash).ToArray();
            actual.ShouldBe(request.Expected);
        }
        using var afterContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var movies = await afterContext.DownloadTaskMovieFile.ToListAsync(CancellationToken);
        var episodes = await afterContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken);
        movies.Cast<DownloadTaskFileBase>().Concat(episodes).OrderBy(x => x.Id).Select(x =>
            (x.Id, ParentId: x.ToParentKey()!.Id, x.HashId, x.DownloadStatus, x.SonarrIntegrationId, x.RadarrIntegrationId)).ShouldBe(before);
    }

    private async Task<List<DownloadTaskFileBase>> ArrangeMovieFilesAsync(IReaparrDbContext db, Guid[] owners)
    {
        var files = new List<DownloadTaskFileBase>();
        var movieRoots = await db.DownloadTaskMovie.AsTracking().Include(x => x.Children)
            .OrderBy(x => x.Id).ToListAsync(CancellationToken);
        movieRoots.Count.ShouldBe(2);
        for (var index = 0; index < movieRoots.Count; index++)
        {
            var root = movieRoots[index];
            var file = root.Children.Single();
            root.RadarrIntegrationId = owners[index];
            file.RadarrIntegrationId = owners[index];
            file.FileName = index == 0 ? "owned-movie.mkv" : "foreign-movie.mkv";
            file.DataTotal = 1201 + index;
            file.DownloadStatus = index == 0 ? DownloadStatus.Completed : DownloadStatus.Paused;
            db.Entry(file).CurrentValues.SetValues(new { HashId = index == 0 ? MovieHash : ForeignMovieHash });
            files.Add(file);
        }
        return files;
    }

    private async Task<List<DownloadTaskFileBase>> ArrangeEpisodeFilesAsync(IReaparrDbContext db, Guid[] owners)
    {
        var files = new List<DownloadTaskFileBase>();
        var tvRoots = await db.DownloadTaskTvShow.AsTracking().IncludeAll().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        tvRoots.Count.ShouldBe(2);
        for (var index = 0; index < tvRoots.Count; index++)
        {
            var root = tvRoots[index];
            var season = root.Children.Single();
            var episode = season.Children.Single();
            var file = episode.Children.Single();
            foreach (var node in new DownloadTaskBase[] { root, season, episode, file })
                node.RadarrIntegrationId = owners[index];
            file.FileName = index == 0 ? "owned-episode.mkv" : "foreign-episode.mkv";
            file.DataTotal = 2301 + index;
            file.DownloadStatus = index == 0 ? DownloadStatus.Downloading : DownloadStatus.Queued;
            db.Entry(file).CurrentValues.SetValues(new { HashId = index == 0 ? EpisodeHash : ForeignEpisodeHash });
            files.Add(file);
        }
        return files;
    }

    private static TorrentWire ExpectedWire(DownloadTaskFileBase file, string hash, string state) => new(
        hash, state, Category, file.DownloadDirectory, Path.Combine(file.DownloadDirectory, file.FileName), file.FileName, file.DataTotal);

    private sealed record TorrentWire(
        string? Hash, string? State, string? Category, string? SavePath, string? ContentPath, string? Name, long Size);
}
