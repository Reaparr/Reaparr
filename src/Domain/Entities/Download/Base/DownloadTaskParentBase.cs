namespace Reaparr.Domain;

public abstract class DownloadTaskParentBase : DownloadTaskBase, IDownloadTaskProgress
{
    /// <summary>
    /// Gets or sets the release year of the media.
    /// </summary>
    [Column(Order = 8)]
    public required int Year { get; init; }

    #region Helpers

    /// <summary>
    /// Gets or sets the total size received of the file in bytes.
    /// </summary>
    [NotMapped]
    public required long DataReceived { get; set; }

    /// <summary>
    /// Gets or sets the total size received of the file in bytes.
    /// </summary>
    [NotMapped]
    public required long FileDataTransferred { get; set; }

    /// <summary>
    /// Gets or sets the total size of the file in bytes.
    /// </summary>
    [NotMapped]
    public required long DataTotal { get; set; }

    /// <summary>
    /// Gets or sets the percentage of the data received from the DataTotal.
    /// </summary>
    [NotMapped]
    public decimal Percentage { get; set; }

    /// <summary>
    /// Gets or sets get the download speeds in bytes per second.
    /// </summary>
    [NotMapped]
    public required long DownloadSpeed { get; set; }

    /// <summary>
    /// Gets or sets the file transfer speeds when the finished download is being merged/moved.
    /// </summary>
    [NotMapped]
    public required long FileTransferSpeed { get; set; }

    [NotMapped]
    public int TimeRemaining { get; set; }

    /// <summary>
    /// Resolves this task's directories from loaded file metadata.
    /// Returns empty paths when no descendant file is loaded.
    /// </summary>
    public (string DownloadDirectory, string DestinationDirectory) GetDirectories()
    {
        var directoryMeta = GetDirectoryMeta();
        return directoryMeta is null
            ? (string.Empty, string.Empty)
            : (
                directoryMeta.GetDownloadDirectory(DownloadTaskType),
                directoryMeta.GetDestinationDirectory(DownloadTaskType)
            );
    }

    private DownloadTaskDirectory? GetDirectoryMeta()
    {
        IEnumerable<DownloadTaskBase> children = this switch
        {
            DownloadTaskMovie x => x.Children,
            DownloadTaskTvShow x => x.Children,
            DownloadTaskTvShowSeason x => x.Children,
            DownloadTaskTvShowEpisode x => x.Children,
            DownloadTaskPhotoAlbum x => x.Children,
            DownloadTaskPhotoImage x => x.Children,
            DownloadTaskMusicArtist x => x.Children,
            DownloadTaskMusicAlbum x => x.Children,
            DownloadTaskMusicTrack x => x.Children,
            DownloadTaskOtherVideo x => x.Children,
            _ => throw new ArgumentOutOfRangeException(nameof(DownloadTaskType)),
        };
        foreach (var child in children)
        {
            var directoryMeta = child switch
            {
                DownloadTaskFileBase file => file.DirectoryMeta,
                DownloadTaskParentBase parent => parent.GetDirectoryMeta(),
                _ => throw new ArgumentOutOfRangeException(nameof(child)),
            };
            if (directoryMeta is not null)
                return directoryMeta;
        }

        return null;
    }

    #endregion
}
