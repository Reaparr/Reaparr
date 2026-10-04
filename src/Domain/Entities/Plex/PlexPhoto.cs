namespace Reaparr.Domain;

public class PlexPhoto : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.Photos;

    public required int PlexPhotoAlbumId { get; set; }

    public PlexPhotoAlbum? PlexPhotoAlbum { get; set; }

    public ICollection<PlexPhotoMediaData> MediaDataList { get; set; } = [];
}
