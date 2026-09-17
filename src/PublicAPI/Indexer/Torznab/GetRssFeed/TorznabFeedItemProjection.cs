using System.Security.Cryptography;
using System.Text;
using Flurl;
using Reaparr.Application.Contracts;

namespace Reaparr.PublicAPI;

public sealed record TorznabFeedItemProjection
{
    public required PlexMediaType MediaType { get; init; }
    public required int MediaId { get; init; }

    /// <summary>
    /// Database ID used to load genres: movie ID for movies, TV-show ID for episodes.
    /// </summary>
    public required int GenreOwnerId { get; init; }

    public required int DataId { get; init; }
    public required int PlexServerId { get; init; }
    public required string PlexServerMachineIdentifier { get; init; }
    public required int PlexLibraryId { get; init; }
    public required int PlexApiRatingKey { get; init; }
    public required int PlexApiMediaId { get; init; }
    public required int PlexApiPartId { get; init; }
    public required string Title { get; init; }
    public required DateTime AddedAt { get; init; }
    public required long Size { get; init; }
    public required VideoQuality Quality { get; init; }
    public required VideoQuality VideoResolution { get; init; }
    public required ReleaseSource Source { get; init; }
    public required string VideoCodec { get; init; }
    public required string AudioCodec { get; init; }
    public required int? SeasonNumber { get; init; }
    public required int? EpisodeNumber { get; init; }
    public required int? TvdbId { get; init; }
    public required int? TmdbId { get; init; }
    public required string? ImdbId { get; init; }
    public required IReadOnlyCollection<PlexGenreType> GenreTypes { get; init; }

    public string CreateStableId()
    {
        const string identityVersion = "v1";
        var identity = FormattableString.Invariant(
            $"{identityVersion}:{PlexServerMachineIdentifier}:{(int)MediaType}:{PlexApiRatingKey}:{PlexApiMediaId}:{PlexApiPartId}"
        );
        return "reaparr-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    public TorznabItem ToTorznabItem(
        IntegrationIdentity integration,
        string torznabApiKey,
        string baseUrl,
        IReadOnlySet<string>? requestedAttributes = null,
        bool includeDebugAttributes = false
    )
    {
        var torrentMetadata = new TorrentMetadataDTO
        {
            Type = MediaType,
            MediaId = MediaId,
            DataId = DataId,
            PartId = DataId,
            PlexApiPartId = PlexApiPartId,
            Quality = Quality,
            LibraryId = PlexLibraryId,
            ServerId = PlexServerId,
        };
        string downloadUrl = baseUrl
            .AppendPathSegment(
                PublicApiRoutes.DownloadTorrent.Replace("{integrationId:guid}", integration.Id.ToString())
            )
            .SetQueryParams(torrentMetadata.Values)
            .SetQueryParam(IntegrationDefinitions.INDEXER_API_KEY, torznabApiKey, isEncoded: false);
        var categories = this.ToTorznabCategories();
        var item = new TorznabItem
        {
            Title = Title,
            PubDate = AddedAt.ToUniversalTime().ToString("R"),
            Guid = new TorznabGuid { Value = CreateStableId() },
            Link = downloadUrl,
            Size = Size,
            Enclosure = new TorznabEnclosure
            {
                Url = downloadUrl,
                Length = Size,
                Type = "application/x-bittorrent",
            },
            SortAddedAt = AddedAt,
            SortMachineIdentifier = PlexServerMachineIdentifier,
            SortMediaType = MediaType,
            SortRatingKey = PlexApiRatingKey,
            SortMediaId = PlexApiMediaId,
            SortPartId = PlexApiPartId,
        };

        item.Attributes.Add(new TorznabAttr("size", Size.ToString()));
        foreach (var category in categories.Distinct())
            item.Attributes.Add(new TorznabAttr("category", ((int)category).ToString()));

        var memeCount = MemeNumberGenerator.GetRandomMemeNumber().ToString();
        item.Attributes.Add(new TorznabAttr("seeders", memeCount));
        item.Attributes.Add(new TorznabAttr("peers", memeCount));

        item.Attributes.Add(new TorznabAttr("type", MediaType == PlexMediaType.Movie ? "movie" : "series"));
        // TODO: Derive language from Plex media stream metadata when language-specific stream data is available.
        item.Attributes.Add(new TorznabAttr("language", "English"));
        item.Attributes.Add(new TorznabAttr("downloadvolumefactor", "0.0"));
        item.Attributes.Add(new TorznabAttr("uploadvolumefactor", "1.0"));
        item.Attributes.Add(new TorznabAttr("resolution", VideoResolution.ToResolutionLabel()));
        item.Attributes.Add(new TorznabAttr("source", Source.ToEnumMemberValue()));
        item.Attributes.Add(new TorznabAttr("videoCodec", VideoCodec));
        item.Attributes.Add(new TorznabAttr("audioCodec", AudioCodec));

        AddOptionalAttribute(item, "season", SeasonNumber);
        AddOptionalAttribute(item, "episode", EpisodeNumber);
        AddOptionalAttribute(item, "tvdbid", TvdbId);
        AddOptionalAttribute(item, "tmdbid", TmdbId);

        if (!string.IsNullOrEmpty(ImdbId))
            item.Attributes.Add(new TorznabAttr("imdb", ImdbId));

        if (includeDebugAttributes)
        {
            item.Attributes.Add(new TorznabAttr("debug-plexServerId", PlexServerId.ToString()));
            item.Attributes.Add(new TorznabAttr("debug-plexLibraryId", PlexLibraryId.ToString()));
            item.Attributes.Add(new TorznabAttr("debug-plexApiMediaId", PlexApiMediaId.ToString()));
            item.Attributes.Add(new TorznabAttr("debug-ratingKey", PlexApiRatingKey.ToString()));
        }

        if (requestedAttributes is not null)
            item.Attributes = item
                .Attributes.Where(x => x.Name is "size" or "category" || requestedAttributes.Contains(x.Name))
                .ToList();

        return item;
    }

    private static void AddOptionalAttribute(TorznabItem item, string name, int? value)
    {
        if (value.HasValue)
            item.Attributes.Add(new TorznabAttr(name, value.Value.ToString()));
    }
}
