namespace Reaparr.Domain;

public class PlexOtherVideo : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.OtherVideos;

    public ICollection<PlexOtherVideoMediaData> MediaDataList { get; init; } = [];
}
