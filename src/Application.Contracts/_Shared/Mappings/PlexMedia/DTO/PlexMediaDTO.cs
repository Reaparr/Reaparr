namespace Reaparr.Application.Contracts;

public record PlexMediaDTO : PlexMediaSlimDTO
{
    public required bool HasArt { get; set; }

    public required bool HasTheme { get; set; }

    public required string Studio { get; set; }

    public required string Summary { get; set; }

    public required string? ContentRating { get; set; }

    public required double Rating { get; set; }

    public required DateTime? OriginallyAvailableAt { get; set; }

    public required int? DiscNumber { get; set; }

    public required int? TrackNumber { get; set; }

    public required int TvShowId { get; set; }

    public required int TvShowSeasonId { get; set; }

    public required List<PlexMediaDataDTO> MediaData { get; set; } = [];

    public required List<PlexMediaDTO> Children { get; set; } = [];
}
