namespace Reaparr.Domain;

public class PlexOtherVideoMediaData : BasePlexMediaData
{
    public required int PlexOtherVideoId { get; set; }

    public PlexOtherVideo? PlexOtherVideo { get; set; }

    public ICollection<PlexOtherVideoDataStream> Streams { get; init; } = [];

    public required int PartIndex { get; set; }

    public override PlexMediaType Type => PlexMediaType.OtherVideos;
}
