namespace Reaparr.Domain;

public class PlexPhotoAlbum : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.PhotoAlbum;

    public ICollection<PlexPhoto> Photos { get; set; } = [];
}
