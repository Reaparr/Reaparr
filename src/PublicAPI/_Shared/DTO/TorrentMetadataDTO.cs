namespace Reaparr.PublicAPI;

public record TorrentMetadataDTO
{
    /// <summary>
    /// The type of Plex media (e.g., Movie, TvShow, Season, Episode).
    /// </summary>
    [QueryParam]
    public required PlexMediaType Type { get; init; }

    /// <summary>
    /// The internal database ID of the media entity (e.g., Movie, TvShow, Season, or Episode).
    /// </summary>
    [QueryParam]
    public required int MediaId { get; init; }

    /// <summary>
    /// The internal database ID of the associated Plex media data entity.
    /// </summary>
    [QueryParam]
    public required int DataId { get; init; }

    /// <summary>
    /// The internal database ID of the media part/file within Reaparr.
    /// </summary>
    [QueryParam]
    public required int PartId { get; init; }

    /// <summary>
    /// The Plex rating key (external ID) for the media part from the Plex API.
    /// </summary>
    [QueryParam]
    public required int PlexApiPartId { get; init; }

    /// <summary>
    /// The desired video quality for the torrent download.
    /// </summary>
    [QueryParam]
    public required VideoQuality VideoQuality { get; init; }

    /// <summary>
    /// The internal database ID of the Plex library containing this media.
    /// </summary>
    [QueryParam]
    public required int LibraryId { get; init; }

    /// <summary>
    /// The internal database ID of the Plex server hosting this media.
    /// </summary>
    [QueryParam]
    public required int ServerId { get; init; }

    public Dictionary<string, string> Values =>
        new()
        {
            { nameof(Type), Type.ToString() },
            { nameof(MediaId), MediaId.ToString() },
            { nameof(DataId), DataId.ToString() },
            { nameof(PartId), PartId.ToString() },
            { nameof(PlexApiPartId), PlexApiPartId.ToString() },
            { nameof(VideoQuality), VideoQuality.ToString() },
            { nameof(LibraryId), LibraryId.ToString() },
            { nameof(ServerId), ServerId.ToString() },
        };
}
