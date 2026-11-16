namespace Reaparr.Application.Contracts;

/// <summary>
/// Media identity and quality projected by torrent-generation and download-submission slices for telemetry.
/// </summary>
/// <remarks>
/// Contains only the fields recorded by <see cref="ITelemetry"/>; it is not a torrent API request or query-string model.
/// </remarks>
public sealed record TorrentMetadataTelemetryDTO
{
    /// <summary>The kind of Plex media identified by <see cref="MediaId"/>.</summary>
    public required PlexMediaType Type { get; init; }

    /// <summary>The internal Reaparr ID of the media entity.</summary>
    public required int MediaId { get; init; }

    /// <summary>The internal Reaparr ID of the media data associated with the selected version.</summary>
    public required int DataId { get; init; }

    /// <summary>The internal Reaparr ID of the media part/file.</summary>
    public required int PartId { get; init; }

    /// <summary>The part identifier supplied by Plex, distinct from the internal <see cref="PartId"/>.</summary>
    public required int PlexApiPartId { get; init; }

    /// <summary>The video quality recorded in the torrent metadata.</summary>
    public required VideoQuality VideoQuality { get; init; }

    /// <summary>The internal Reaparr ID of the library containing the media.</summary>
    public required int LibraryId { get; init; }

    /// <summary>The internal Reaparr ID of the server hosting the media.</summary>
    public required int ServerId { get; init; }
}
