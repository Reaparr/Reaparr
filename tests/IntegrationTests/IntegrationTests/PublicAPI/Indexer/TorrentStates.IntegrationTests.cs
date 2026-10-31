using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Reaparr.IntegrationTests;

public class TorrentStatesIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(IntegrationType.Radarr)]
    [Arguments(IntegrationType.Sonarr)]
    public async Task ShouldReturnExactWireStatesForOwnedFilesAndPreserveForeignTasks(IntegrationType type)
    {
        // Arrange
        (DownloadStatus Status, string WireState)[] states =
        [
            (DownloadStatus.AuthError, "error"),
            (DownloadStatus.StorageError, "error"),
            (DownloadStatus.SourceUnavailable, "error"),
            (DownloadStatus.DownloadClientError, "error"),
            (DownloadStatus.IntegrityError, "error"),
            (DownloadStatus.Restarting, "queuedDL"),
            (DownloadStatus.Downloading, "downloading"),
            (DownloadStatus.Queued, "queuedDL"),
            (DownloadStatus.Moving, "moving"),
            (DownloadStatus.Paused, "pausedDL"),
            (DownloadStatus.Completed, "pausedUP"),
            (DownloadStatus.Unknown, "stalledDL"),
        ];
        var isMovie = type == IntegrationType.Radarr;
        using var container = await CreateContainer(
            new Seed(8638),
            config =>
            {
                config.DatabaseOptions = database =>
                {
                    database.PlexServerCount = 1;
                    database.MovieDownloadTasksCount = isMovie ? states.Length + 1 : 0;
                    database.TvShowDownloadTasksCount = isMovie ? 0 : states.Length + 1;
                    database.TvShowSeasonDownloadTasksCount = isMovie ? 0 : 1;
                    database.TvShowEpisodeDownloadTasksCount = isMovie ? 0 : 1;
                    database.RadarrIntegrationCount = isMovie ? 2 : 0;
                    database.SonarrIntegrationCount = isMovie ? 0 : 2;
                };
            }
        );
        var dbContext = container.DbContext;
        var ownerIds = isMovie
            ? await dbContext.RadarrIntegrations.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken)
            : await dbContext.SonarrIntegrations.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        ownerIds.Count.ShouldBe(2);
        var integrationId = ownerIds[0];
        var apiKey = isMovie
            ? await dbContext
                .RadarrIntegrations.Where(x => x.Id == integrationId)
                .Select(x => x.QBittorrentApiKey)
                .SingleAsync(CancellationToken)
            : await dbContext
                .SonarrIntegrations.Where(x => x.Id == integrationId)
                .Select(x => x.QBittorrentApiKey)
                .SingleAsync(CancellationToken);
        var category = isMovie
            ? await dbContext
                .RadarrIntegrations.Where(x => x.Id == integrationId)
                .Select(x => x.Category)
                .SingleAsync(CancellationToken)
            : await dbContext
                .SonarrIntegrations.Where(x => x.Id == integrationId)
                .Select(x => x.Category)
                .SingleAsync(CancellationToken);
        var files = new List<DownloadTaskFileBase>();
        if (isMovie)
        {
            var roots = await dbContext
                .DownloadTaskMovie.AsTracking()
                .Include(x => x.Children)
                .OrderBy(x => x.Id)
                .ToListAsync(CancellationToken);
            foreach (var root in roots)
            {
                var owner = files.Count == states.Length ? ownerIds[1] : integrationId;
                root.RadarrIntegrationId = owner;
                var file = root.Children.Single();
                file.RadarrIntegrationId = owner;
                files.Add(file);
            }
        }
        else
        {
            var roots = await dbContext
                .DownloadTaskTvShow.AsTracking()
                .IncludeAll()
                .OrderBy(x => x.Id)
                .ToListAsync(CancellationToken);
            foreach (var root in roots)
            {
                var owner = files.Count == states.Length ? ownerIds[1] : integrationId;
                var season = root.Children.Single();
                var episode = season.Children.Single();
                var file = episode.Children.Single();
                foreach (var node in new DownloadTaskBase[] { root, season, episode, file })
                    node.SonarrIntegrationId = owner;
                files.Add(file);
            }
        }
        files.Count.ShouldBe(states.Length + 1);
        for (var index = 0; index < files.Count; index++)
        {
            dbContext.Entry(files[index]).CurrentValues.SetValues(new { HashId = $"{index + 1:x40}" });
            files[index].DownloadStatus = index < states.Length ? states[index].Status : DownloadStatus.StorageError;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServers.ExecuteUpdateAsync(
            update =>
                update.SetProperty(server => server.IsEnabled, true).SetProperty(server => server.OwnedOverride, false),
            CancellationToken
        );
        var before = files
            .OrderBy(file => file.Id)
            .Select(file =>
                (
                    file.Id,
                    ParentId: file.ToParentKey()!.Id,
                    file.HashId,
                    file.DownloadStatus,
                    file.SonarrIntegrationId,
                    file.RadarrIntegrationId
                )
            )
            .ToArray();
        using var client = container.GetApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Act
        using var response = await client.GetAsync(
            $"/api/public/integrations/{integrationId}/download-client/api/v2/torrents/info?hashes=all",
            CancellationToken
        );

        // Assert — inspect actual qBittorrent JSON names/values, not an internal mapper call.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        var returned = document
            .RootElement.EnumerateArray()
            .Select(item =>
                (
                    Hash: item.GetProperty("hash").GetString(),
                    State: item.GetProperty("state").GetString(),
                    Category: item.GetProperty("category").GetString(),
                    RatioLimit: item.GetProperty("ratio_limit").GetSingle()
                )
            )
            .OrderBy(item => item.Hash)
            .ToArray();
        returned.ShouldBe(
            states
                .Select(
                    (state, index) =>
                        (
                            Hash: files[index].HashId,
                            State: (string?)state.WireState,
                            Category: (string?)category,
                            RatioLimit: state.Status == DownloadStatus.Completed ? 0f : -2f
                        )
                )
                .OrderBy(item => item.Hash)
                .ToArray()
        );
        using var afterContext = container.DbContext;
        var afterFiles = isMovie
            ? (
                await afterContext.DownloadTaskMovieFile.OrderBy(file => file.Id).ToListAsync(CancellationToken)
            ).Cast<DownloadTaskFileBase>()
            : (
                await afterContext.DownloadTaskTvShowEpisodeFile.OrderBy(file => file.Id).ToListAsync(CancellationToken)
            ).Cast<DownloadTaskFileBase>();
        afterFiles
            .Select(file =>
                (
                    file.Id,
                    ParentId: file.ToParentKey()!.Id,
                    file.HashId,
                    file.DownloadStatus,
                    file.SonarrIntegrationId,
                    file.RadarrIntegrationId
                )
            )
            .ShouldBe(before);
    }
}
