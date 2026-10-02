namespace Reaparr.Domain;

public class MediaOverviewOtherVideoSnapshot : BaseMediaOverviewSnapshot
{
    public required int PlexOtherVideoId { get; set; }

    public PlexOtherVideo? PlexOtherVideo { get; set; }
}
