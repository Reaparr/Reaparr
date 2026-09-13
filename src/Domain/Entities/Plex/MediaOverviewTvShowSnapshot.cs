namespace Reaparr.Domain;

public class MediaOverviewTvShowSnapshot : BaseEntity
{
    public required int TitleRank { get; set; }

    public required int YearRank { get; set; }

    public required int AddedAtRank { get; set; }

    public required int UpdatedAtRank { get; set; }

    public required int DurationRank { get; set; }

    public required int MediaSizeRank { get; set; }

    public required int QualityRank { get; set; }

    #region Relationships

    public PlexTvShow? PlexTvShow { get; set; }

    public required int PlexTvShowId { get; set; }

    public PlexLibrary? PlexLibrary { get; set; }

    public required int PlexLibraryId { get; set; }

    #endregion

    [NotMapped]
    public string SearchTitle { get; set; } = string.Empty;

    [NotMapped]
    public int Year { get; set; }

    [NotMapped]
    public DateTime AddedAt { get; set; }

    [NotMapped]
    public DateTime? UpdatedAt { get; set; }

    [NotMapped]
    public int Duration { get; set; }

    [NotMapped]
    public long MediaSize { get; set; }

    [NotMapped]
    public VideoQuality Quality { get; set; }
}
