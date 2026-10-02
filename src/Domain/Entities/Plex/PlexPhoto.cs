namespace Reaparr.Domain;

public class PlexPhoto : BasePlexMedia
{
    public required int PlexPhotoAlbumId { get; set; }

    public PlexPhotoAlbum? PlexPhotoAlbum { get; set; }

    public ICollection<PlexPhotoMediaData> MediaDataList { get; set; } = [];
}
