namespace Reaparr.Domain;

public abstract class BaseMediaOverviewSnapshot : BaseEntity
{
    public required int PlexLibraryId { get; set; }

    public PlexLibrary? PlexLibrary { get; set; }

    public required int TitleRank { get; set; }

    public required int YearRank { get; set; }

    public required int AddedAtRank { get; set; }

    public required int UpdatedAtRank { get; set; }

    public required int DurationRank { get; set; }

    public required int MediaSizeRank { get; set; }
}
