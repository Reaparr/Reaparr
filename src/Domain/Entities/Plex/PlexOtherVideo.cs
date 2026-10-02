namespace Reaparr.Domain;

public class PlexOtherVideo : BasePlexMedia
{
    public ICollection<PlexOtherVideoMediaData> MediaDataList { get; init; } = [];
}
