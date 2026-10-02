namespace Reaparr.Domain;

public class PlexPhotoAlbum : BaseEntity
{
    public required int PlexApiRatingKey { get; set; }

    public required string Title { get; set; }

    public string SearchTitle { get; set; } = string.Empty;

    public int SortIndex { get; set; }

    public string? Summary { get; set; }

    public int? Year { get; set; }

    public int? Duration { get; set; }

    public long MediaSize { get; set; }

    public DateTime AddedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public required int PlexLibraryId { get; set; }

    public PlexLibrary? PlexLibrary { get; set; }

    public required int PlexServerId { get; set; }

    public PlexServer? PlexServer { get; set; }

    public ICollection<PlexPhoto> Photos { get; set; } = [];
}
