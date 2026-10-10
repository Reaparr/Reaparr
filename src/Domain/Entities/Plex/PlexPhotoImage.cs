namespace Reaparr.Domain;

public class PlexPhotoImage : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.PhotoImage;

    /// <summary>
    /// The Plex key of the <see cref="PlexTvShow"/> this belongs too.
    /// </summary>
    public required int ParentKey { get; set; }

    public required int PlexPhotoAlbumId { get; set; }

    public PlexPhotoAlbum? PlexPhotoAlbum { get; set; }

    public ICollection<PlexPhotoMediaData> MediaDataList { get; set; } = [];
}
