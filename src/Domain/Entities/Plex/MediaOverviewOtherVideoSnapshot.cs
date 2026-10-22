namespace Reaparr.Domain;

public class MediaOverviewOtherVideoSnapshot : BaseMediaOverviewSnapshot
{
    public required int QualityRank { get; set; }

    public required int YearRank { get; set; }

    public int Year { get; set; }

    public VideoQuality VideoQuality { get; set; }

    public required int PlexOtherVideoId { get; set; }

    public PlexOtherVideo? PlexOtherVideo { get; set; }
}
