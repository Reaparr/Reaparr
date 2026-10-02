namespace Reaparr.Domain;

public class MediaOverviewPhotoAlbumSnapshot : BaseMediaOverviewSnapshot
{
    public required int PlexPhotoAlbumId { get; set; }

    public PlexPhotoAlbum? PlexPhotoAlbum { get; set; }
}
