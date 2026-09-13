namespace Reaparr.Domain;

public class MediaOverviewMovieSnapshot : BaseEntity
{
    public required int PlexMovieId { get; set; }

    public required int PlexLibraryId { get; set; }

    public required int TitleRank { get; set; }

    public required int YearRank { get; set; }

    public required int AddedAtRank { get; set; }

    public required int UpdatedAtRank { get; set; }

    public required int DurationRank { get; set; }

    public required int MediaSizeRank { get; set; }

    public required int QualityRank { get; set; }

    public PlexMovie? PlexMovie { get; set; }
}
