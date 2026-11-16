namespace Reaparr.Domain;

/// <summary>
/// A persisted Photo Folder grouping in the download queue, organized by folders rather than albums.
/// Root rows group a server/library's downloads; captured paths keep queue placement independent of catalog refreshes.
/// </summary>
public class DownloadTaskPhotoFolder : DownloadTaskParentBase
{
    /// <summary>
    /// ID of the containing queue folder, or null for a server/library root.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Navigation to the containing queue folder; absent for a root.
    /// </summary>
    public DownloadTaskPhotoFolder? Parent { get; set; }

    /// <summary>
    /// Immediate child folder tasks.
    /// </summary>
    public required ICollection<DownloadTaskPhotoFolder> Children { get; set; } = [];

    /// <summary>
    /// Image and clip file tasks placed directly in this queue folder.
    /// </summary>
    public required ICollection<DownloadTaskPhotoImageFile> Files { get; set; } = [];

    /// <summary>
    /// Folder path relative to the server/library queue root; empty for the root row.
    /// </summary>
    public required string RelativePath { get; set; }

    /// <summary>
    /// Base directory for temporary photo downloads captured when this folder task is created.
    /// </summary>
    public required string DownloadRootPath { get; set; }

    /// <summary>
    /// Base destination directory captured when this folder task is created.
    /// </summary>
    public required string DestinationRootPath { get; set; }

    /// <summary>
    /// Non-persisted enumeration of immediate folder and file children for queue traversal.
    /// </summary>
    [NotMapped]
    public IEnumerable<DownloadTaskBase> ChildrenWithFiles => Children.Cast<DownloadTaskBase>().Concat(Files);

    /// <summary>
    /// Non-persisted photo-family media discriminator used for this folder grouping.
    /// </summary>
    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.PhotoAlbum; // TODO : Should this be PhotoFolder?

    /// <summary>
    /// Non-persisted task discriminator identifying this row as a Photo Folder.
    /// </summary>
    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.PhotoFolder;

    /// <summary>
    /// False because folders group downloads; only their file tasks perform transfers.
    /// </summary>
    [NotMapped]
    public override bool IsDownloadable => false;

    /// <summary>
    /// Number of nodes in the loaded subtree, including this folder, descendant folders, and file tasks.
    /// </summary>
    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + Files.Sum(x => x.Count) + 1;

    /// <summary>
    /// Creates the containing folder's queue key, or returns null for a root folder.
    /// </summary>
    public override DownloadTaskKey? ToParentKey() =>
        ParentId is not { } parentId
            ? null
            : new()
            {
                Type = DownloadTaskType.PhotoFolder,
                Id = parentId,
                PlexServerId = PlexServerId,
                PlexLibraryId = PlexLibraryId,
            };
}
