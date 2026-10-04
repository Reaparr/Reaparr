namespace Reaparr.Domain;

public class MediaOverviewOtherVideoSnapshot : BaseMediaOverviewSnapshot
{
    public required int QualityRank { get; set; }

    [NotMapped]
    public VideoQuality Quality { get; set; }

    public required int PlexOtherVideoId { get; set; }

    public PlexOtherVideo? PlexOtherVideo { get; set; }
}
