namespace Reaparr.Domain;

public abstract class BaseMediaOverviewSnapshot : BaseEntity
{
    public required int PlexLibraryId { get; set; }

    public PlexLibrary? PlexLibrary { get; set; }

    /// <summary>
    /// Normalized title retained for snapshot ordering and filtering metadata.
    /// </summary>
    public string SearchTitle { get; set; } = string.Empty;

    /// <summary>
    /// Precomputed title navigation bucket used by media overview pages.
    /// </summary>
    public string NavigationTitleLabel { get; set; } = "#";

    public int Duration { get; set; }

    public DateTime AddedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long MediaSize { get; set; }

    public required int TitleRank { get; set; }

    public required int AddedAtRank { get; set; }

    public required int UpdatedAtRank { get; set; }

    public required int DurationRank { get; set; }

    public required int MediaSizeRank { get; set; }
}
