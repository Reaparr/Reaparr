using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using Reaparr.PublicAPI;

namespace Reaparr.IntegrationTests;

public class TorrentIdentityIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.Episode)]
    public async Task ShouldRoundTripTorrent_WhenMetadataMatchesSource(PlexMediaType mediaType)
    {
        // Arrange
        using var container = await CreatePausedContainer(1, 0);
        var dbContext = container.DbContext;
        var (metadata, source) = await GetSource(dbContext, mediaType);
        var integration =
            mediaType == PlexMediaType.Movie
                ? await dbContext
                    .RadarrIntegrations.Select(x => new
                    {
                        x.Id,
                        x.TorznabApiKey,
                        x.QBittorrentApiKey,
                        x.Category,
                    })
                    .SingleAsync(CancellationToken)
                : await dbContext
                    .SonarrIntegrations.Select(x => new
                    {
                        x.Id,
                        x.TorznabApiKey,
                        x.QBittorrentApiKey,
                        x.Category,
                    })
                    .SingleAsync(CancellationToken);
        using var client = container.GetApiClient();

        // Act
        using var generation = await client.GetAsync(
            BuildDownloadUrl(integration.Id, integration.TorznabApiKey, metadata),
            CancellationToken
        );
        var bytes = await generation.Content.ReadAsByteArrayAsync(CancellationToken);
        var torrent = new BencodeParser().Parse<Torrent>(new MemoryStream(bytes));
        var hash = torrent.GetInfoHash();
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(bytes), "torrents", "release.torrent");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            integration.QBittorrentApiKey
        );
        using var addition = await client.PostAsync(
            $"/api/public/integrations/{integration.Id}/download-client/api/v2/torrents/add",
            upload,
            CancellationToken
        );
        using var info = await client.GetAsync(
            $"/api/public/integrations/{integration.Id}/download-client/api/v2/torrents/info?hashes={hash}",
            CancellationToken
        );

        // Assert
        generation.StatusCode.ShouldBe(HttpStatusCode.OK);
        generation.Content.Headers.ContentType!.MediaType.ShouldBe("application/x-bittorrent");
        torrent.File.FileName.ShouldBe($"{source.GetFileName}.torrent");
        torrent.File.FileSize.ShouldBe(source.Size);
        addition.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await addition.Content.ReadAsStringAsync(CancellationToken)).ShouldBe("Ok.");
        using var after = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var files = await ReadFiles(after, mediaType);
        files
            .Select(x =>
                (
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.PlexLibraryId,
                    x.PlexServerId,
                    x.SonarrIntegrationId,
                    x.RadarrIntegrationId,
                    x.HashId
                )
            )
            .ShouldBe([
                (
                    source.PlexApiRatingKey,
                    source.PlexApiMediaId,
                    source.PlexApiPartId,
                    source.PlexLibraryId,
                    source.PlexServerId,
                    mediaType == PlexMediaType.Episode ? (Guid?)integration.Id : null,
                    mediaType == PlexMediaType.Movie ? (Guid?)integration.Id : null,
                    (string?)hash
                ),
            ]);
        var parentIds =
            mediaType == PlexMediaType.Movie
                ? await after.DownloadTaskMovie.Select(x => x.Id).ToArrayAsync(CancellationToken)
                : await after.DownloadTaskTvShowEpisode.Select(x => x.Id).ToArrayAsync(CancellationToken);
        files.Select(GetParentId).ShouldBe(parentIds);
        (
            await ReadFiles(after, mediaType == PlexMediaType.Movie ? PlexMediaType.Episode : PlexMediaType.Movie)
        ).ShouldBeEmpty();
        info.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var infoJson = JsonDocument.Parse(await info.Content.ReadAsStringAsync(CancellationToken));
        infoJson
            .RootElement.EnumerateArray()
            .Select(x =>
                (
                    x.GetProperty("hash").GetString(),
                    x.GetProperty("state").GetString(),
                    x.GetProperty("category").GetString()
                )
            )
            .ShouldBe([((string?)hash, (string?)"queuedDL", (string?)integration.Category)]);
    }

    [Test]
    public async Task ShouldRejectMixedMovieIdentityAtGenerationAndUpload_WithoutTasksOrControlHashChanges()
    {
        // Arrange
        using var container = await CreatePausedContainer(2, 1);
        var dbContext = container.DbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var sources = await dbContext.PlexMovieData.OrderBy(x => x.Id).ToArrayAsync(CancellationToken);
        sources.Length.ShouldBe(2);
        var source = sources[0];
        var other = sources[1];
        source.PlexMovieId.ShouldNotBe(other.PlexMovieId);
        source.PlexApiPartId.ShouldNotBe(other.PlexApiPartId);
        (source.PlexLibraryId, source.PlexServerId).ShouldBe((other.PlexLibraryId, other.PlexServerId));
        var metadata = CreateMetadata(source, other.PlexMovieId, PlexMediaType.Movie);
        var control = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        control.RadarrIntegrationId = integration.Id;
        control.UpdateInitProperty(nameof(control.HashId), "control-hash");
        control.UpdateInitProperty(nameof(control.PlexApiRatingKey), source.PlexApiRatingKey);
        control.UpdateInitProperty(nameof(control.PlexApiMediaId), source.PlexApiMediaId);
        control.UpdateInitProperty(nameof(control.PlexApiPartId), source.PlexApiPartId);
        var parent = await dbContext.DownloadTaskMovie.AsTracking().SingleAsync(CancellationToken);
        parent.RadarrIntegrationId = integration.Id;
        parent.UpdateInitProperty(nameof(parent.PlexApiRatingKey), source.PlexApiRatingKey);
        await dbContext.SaveChangesAsync(CancellationToken);
        var beforeFiles = (await ReadFiles(dbContext, PlexMediaType.Movie)).Select(ToFileState).ToArray();
        beforeFiles
            .Select(x => (x.HashId, x.RadarrIntegrationId))
            .ShouldBe([((string?)"control-hash", (Guid?)integration.Id)]);
        var beforeParents = await ReadMovieParents(dbContext);
        using var client = container.GetApiClient();

        // Act
        using var generation = await client.GetAsync(
            BuildDownloadUrl(integration.Id, integration.TorznabApiKey, metadata),
            CancellationToken
        );
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(CreateTorrentBytes(metadata)), "torrents", "forged.torrent");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            integration.QBittorrentApiKey
        );
        using var addition = await client.PostAsync(
            $"/api/public/integrations/{integration.Id}/download-client/api/v2/torrents/add",
            upload,
            CancellationToken
        );

        // Assert
        generation.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await generation.Content.ReadAsByteArrayAsync(CancellationToken)).ShouldBeEmpty();
        addition.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var error = JsonDocument.Parse(await addition.Content.ReadAsStringAsync(CancellationToken));
        var errors = error.RootElement.GetProperty("errors").EnumerateArray().ToArray();
        errors.Length.ShouldBe(1);
        errors
            .Single()
            .GetProperty("metadata")
            .GetProperty(nameof(TorrentMetadataDTO.DataId))
            .GetString()
            .ShouldBe(
                "DataId, PartId, MediaId, PlexApiPartId, LibraryId, and ServerId must identify the same media source."
            );
        using var after = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        (await ReadFiles(after, PlexMediaType.Movie)).Select(ToFileState).ShouldBe(beforeFiles);
        (await ReadMovieParents(after)).ShouldBe(beforeParents);
        (await ReadFiles(after, PlexMediaType.Episode)).ShouldBeEmpty();
    }

    private async Task<BaseContainer> CreatePausedContainer(int movieCount, int existingTasks)
    {
        var container = await CreateContainer(
            new Seed(8682),
            config =>
                config.DatabaseOptions = database =>
                {
                    database.PlexServerCount = 1;
                    database.PlexMovieLibraryCount = 1;
                    database.PlexTvShowLibraryCount = 1;
                    database.MovieCount = movieCount;
                    database.MovieDownloadTasksCount = existingTasks;
                    database.TvShowCount = 1;
                    database.TvShowSeasonCount = 1;
                    database.TvShowEpisodeCount = 1;
                    database.RadarrIntegrationCount = 1;
                    database.SonarrIntegrationCount = 1;
                }
        );
        await container.DbContext.PlexServers.ExecuteUpdateAsync(
            update =>
                update
                    .SetProperty(x => x.IsEnabled, true)
                    .SetProperty(x => x.OwnedOverride, false)
                    .SetProperty(x => x.IsDownloadsPausedByUser, true),
            CancellationToken
        );
        return container;
    }

    private async Task<(TorrentMetadataDTO Metadata, BasePlexMediaData Source)> GetSource(
        IReaparrDbContext context,
        PlexMediaType mediaType
    )
    {
        if (mediaType == PlexMediaType.Movie)
        {
            var source = await context.PlexMovieData.SingleAsync(CancellationToken);
            return (CreateMetadata(source, source.PlexMovieId, mediaType), source);
        }
        var episode = await context.PlexTvShowEpisodeData.SingleAsync(CancellationToken);
        return (CreateMetadata(episode, episode.PlexTvShowEpisodeId, mediaType), episode);
    }

    private static TorrentMetadataDTO CreateMetadata(BasePlexMediaData source, int mediaId, PlexMediaType mediaType) =>
        new()
        {
            Type = mediaType,
            MediaId = mediaId,
            DataId = source.Id,
            PartId = source.Id,
            PlexApiPartId = source.PlexApiPartId,
            VideoQuality = source.VideoQuality,
            LibraryId = source.PlexLibraryId,
            ServerId = source.PlexServerId,
        };

    private async Task<DownloadTaskFileBase[]> ReadFiles(IReaparrDbContext context, PlexMediaType mediaType)
    {
        if (mediaType == PlexMediaType.Movie)
            return await context.DownloadTaskMovieFile.OrderBy(x => x.Id).ToArrayAsync(CancellationToken);
        return await context.DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id).ToArrayAsync(CancellationToken);
    }

    private static FileState ToFileState(DownloadTaskFileBase file) =>
        new(
            file.Id,
            GetParentId(file),
            file.HashId,
            file.PlexApiRatingKey,
            file.PlexApiMediaId,
            file.PlexApiPartId,
            file.PlexLibraryId,
            file.PlexServerId,
            file.SonarrIntegrationId,
            file.RadarrIntegrationId
        );

    private static Guid GetParentId(DownloadTaskFileBase file) =>
        file switch
        {
            DownloadTaskMovieFile movie => movie.ParentId,
            DownloadTaskTvShowEpisodeFile episode => episode.ParentId,
            _ => throw new ArgumentOutOfRangeException(nameof(file)),
        };

    private async Task<ParentState[]> ReadMovieParents(IReaparrDbContext context) =>
        (await context.DownloadTaskMovie.OrderBy(x => x.Id).ToArrayAsync(CancellationToken))
            .Select(x => new ParentState(
                x.Id,
                x.PlexApiRatingKey,
                x.PlexLibraryId,
                x.PlexServerId,
                x.RadarrIntegrationId
            ))
            .ToArray();

    private static string BuildDownloadUrl(Guid integrationId, string apiKey, TorrentMetadataDTO metadata)
    {
        var query = string.Join(
            "&",
            metadata.Values.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")
        );
        return $"/api/public/integrations/{integrationId}/indexer/download?{query}&apikey={Uri.EscapeDataString(apiKey)}";
    }

    private static byte[] CreateTorrentBytes(TorrentMetadataDTO metadata)
    {
        var fields = new BDictionary { ["created by"] = new BString("Reaparr") };
        foreach (var value in metadata.Values)
            fields[value.Key] = new BString(value.Value);
        return new Torrent
        {
            Pieces = new byte[20],
            PieceSize = 32768,
            Trackers =
            [
                ["http://tracker.example.com/announce"],
            ],
            File = new SingleFileInfo { FileName = "forged.mkv", FileSize = 1024 },
            ExtraFields = fields,
        }.EncodeAsBytes();
    }

    private sealed record FileState(
        Guid Id,
        Guid ParentId,
        string? HashId,
        int RatingKey,
        int MediaId,
        int PartId,
        int LibraryId,
        int ServerId,
        Guid? SonarrIntegrationId,
        Guid? RadarrIntegrationId
    );

    private sealed record ParentState(Guid Id, int RatingKey, int LibraryId, int ServerId, Guid? RadarrIntegrationId);
}
