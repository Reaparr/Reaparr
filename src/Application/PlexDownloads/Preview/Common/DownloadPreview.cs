namespace Reaparr.Application;

public record DownloadPreview
{
    public required int Id { get; init; }

    public required string Title { get; init; }

    public required long Size { get; set; }

    public required int ChildCount { get; set; }

    public required PlexMediaType MediaType { get; init; } = PlexMediaType.Unknown;

    public required int TvShowId { get; init; }

    public required int SeasonId { get; init; }

    public required int ArtistId { get; init; }

    public required int AlbumId { get; init; }

    public required int PhotoAlbumId { get; init; }

    public required List<PlexMediaQuality> Qualities { get; init; }

    public required List<DownloadPreview> Children { get; init; } = [];
}
