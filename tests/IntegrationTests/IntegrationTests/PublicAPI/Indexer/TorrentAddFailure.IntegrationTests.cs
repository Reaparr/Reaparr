using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;

namespace Reaparr.IntegrationTests;

public class TorrentAddFailureIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldReturnRecognizedFailureWithoutTasksOrHashChanges_WhenUploadedMovieWasRemoved()
    {
        // Arrange
        using var container = await CreateContainer(
            new Seed(8636),
            config =>
            {
                config.DatabaseOptions = database =>
                {
                    database.PlexServerCount = 1;
                    database.PlexAccountCount = 1;
                    database.PlexMovieLibraryCount = 1;
                    database.MovieCount = 1;
                    database.MovieDownloadTasksCount = 1;
                    database.RadarrIntegrationCount = 1;
                };
            }
        );
        var dbContext = container.DbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movie = await dbContext.PlexMovies.SingleAsync(CancellationToken);
        await dbContext.DownloadTaskMovie.ExecuteUpdateAsync(
            update => update.SetProperty(task => task.DownloadStatus, DownloadStatus.Completed),
            CancellationToken
        );
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            update => update.SetProperty(task => task.DownloadStatus, DownloadStatus.Completed),
            CancellationToken
        );
        var before = await dbContext
            .DownloadTaskMovieFile.OrderBy(file => file.Id)
            .Select(file => new
            {
                file.Id,
                file.ParentId,
                file.HashId,
                file.DownloadStatus,
                file.RadarrIntegrationId,
            })
            .ToListAsync(CancellationToken);
        before.Count.ShouldBe(1);
        var parentIds = await dbContext
            .DownloadTaskMovie.OrderBy(task => task.Id)
            .Select(task => task.Id)
            .ToListAsync(CancellationToken);
        parentIds.ShouldBe([before.Single().ParentId]);
        var route = $"/api/public/integrations/{integration.Id}";
        using var client = container.GetApiClient();
        var rssUrl = $"{route}/indexer/api?t=search&cat=2000&limit=1&apikey={integration.TorznabApiKey}";
        using var rssResponse = await client.GetAsync(rssUrl, CancellationToken);
        rssResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var channel = XDocument
            .Parse(await rssResponse.Content.ReadAsStringAsync(CancellationToken))
            .Root!.Element("channel")!;
        var item = channel.Elements("item").Single();
        using var torrentResponse = await client.GetAsync(item.Element("link")!.Value, CancellationToken);
        torrentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        torrentResponse.Content.Headers.ContentType!.MediaType.ShouldBe("application/x-bittorrent");
        var torrentBytes = await torrentResponse.Content.ReadAsByteArrayAsync(CancellationToken);
        (await dbContext.PlexMovies.Where(row => row.Id == movie.Id).ExecuteDeleteAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexMovies.Select(row => row.Id).ToListAsync(CancellationToken)).ShouldBeEmpty();
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(torrentBytes), "torrents", "removed-movie.torrent");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            integration.QBittorrentApiKey
        );

        // Act
        using var response = await client.PostAsync(
            $"{route}/download-client/api/v2/torrents/add",
            upload,
            CancellationToken
        );

        // Assert — exact text is the qBittorrent failure contract used by Arr clients.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(CancellationToken)).ShouldBe("Fails.");
        using var afterContext = container.DbContext;
        (
            await afterContext
                .DownloadTaskMovieFile.OrderBy(file => file.Id)
                .Select(file => new
                {
                    file.Id,
                    file.ParentId,
                    file.HashId,
                    file.DownloadStatus,
                    file.RadarrIntegrationId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(before);
        (
            await afterContext
                .DownloadTaskMovie.OrderBy(task => task.Id)
                .Select(task => task.Id)
                .ToListAsync(CancellationToken)
        ).ShouldBe(parentIds);
        (await container.DownloadTaskScheduler.GetCurrentlyDownloadingKeysByServer(movie.PlexServerId)).ShouldBeEmpty();
        (
            await afterContext.DownloadTaskTvShowEpisodeFile.Select(file => file.Id).ToListAsync(CancellationToken)
        ).ShouldBeEmpty();
    }
}
