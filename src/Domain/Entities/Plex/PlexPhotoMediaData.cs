namespace Reaparr.Domain;

public class PlexPhotoMediaData : BasePlexMediaData
{
    public required int PlexPhotoId { get; set; }

    public PlexPhoto? PlexPhoto { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public override PlexMediaType Type => PlexMediaType.PhotoImage;
}
