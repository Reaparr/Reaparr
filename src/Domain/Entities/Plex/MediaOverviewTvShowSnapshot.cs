namespace Reaparr.Domain;

public class MediaOverviewTvShowSnapshot : BaseEntity
{
    public required int PlexTvShowId { get; init; }

    public required int PlexLibraryId { get; init; }

    public required int TitleRank { get; init; }

    public required int YearRank { get; init; }

    public required int AddedAtRank { get; init; }

    public required int UpdatedAtRank { get; init; }

    public required int DurationRank { get; init; }

    public required int MediaSizeRank { get; init; }

    public required int QualityRank { get; init; }

    public PlexTvShow? PlexTvShow { get; init; }
}
