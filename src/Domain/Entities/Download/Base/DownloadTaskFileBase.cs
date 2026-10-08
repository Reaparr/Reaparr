namespace Reaparr.Domain;

public abstract class DownloadTaskFileBase : DownloadTaskBase, IDownloadTaskProgress, IDownloadFileTransferProgress
{
    /// <summary>
    /// Gets or sets the identifier of the "Metadata.Media.Id" of the media being downloaded.
    /// </summary>
    /// <remarks> This is coming from the Plex API and is used to identify the specific media that is being downloaded.</remarks>
    [Column(Order = 8)]
    public required int PlexApiMediaId { get; init; }

    /// <summary>
    /// Gets or sets the identifier of the "Metadata.Media.Part.Id" of the media being downloaded.
    /// <remarks> This is coming from the Plex API and is used to identify the specific media part/file that is being downloaded.</remarks>
    /// </summary>
    [Column(Order = 9)]
    public required int PlexApiPartId { get; init; }

    [Column(Order = 10)]
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the relative obfuscated URL of the media to be downloaded,
    /// e.g: /library/parts/47660/156234666/file.mkv.
    /// </summary>
    [Column(Order = 11)]
    public required string FileLocationUrl { get; init; }

    /// <summary>
    /// When a download is added through a torrent or nzb, this is the unique hash id of that download.
    /// This is the hash id of the torrent that Sonarr/Radarr uses to keep track of the download.
    /// </summary>
    [Column(Order = 12)]
    public required string? HashId { get; init; }

    /// <summary>
    /// Gets or sets get or sets the media quality of this <see cref="DownloadTaskGeneric"/>.
    /// </summary>
    [Column(Order = 13)]
    public required VideoQuality Quality { get; init; }

    [Column(Order = 14)]
    public required DownloadTaskDirectory DirectoryMeta { get; init; }

    #region Download Progress

    /// <summary>
    /// Gets or sets the total size received of the file in bytes.
    /// </summary>
    [Column(Order = 15)]
    public required long DataReceived { get; set; }

    /// <summary>
    /// Gets or sets the total size of the file in bytes.
    /// </summary>
    [Column(Order = 16)]
    public required long DataTotal { get; set; }

    /// <summary>
    /// Gets or sets get the download speeds in bytes per second.
    /// </summary>
    [Column(Order = 17)]
    public required long DownloadSpeed { get; set; }

    /// <summary>
    /// Gets or sets the snapshot of the DirectDownloader to resume from if set
    /// </summary>
    [Column(Order = 18)]
    public required DirectDownloadSnapshot? DirectDownloadSnapshot { get; set; }

    [Column(Order = 19)]
    public required PlexDownloadClientType DownloadClientType { get; set; } = PlexDownloadClientType.Direct;

    #endregion

    #region File Transfer Progress

    /// <summary>
    /// Gets or sets the file transfer speeds when the finished download is being merged/moved.
    /// </summary>
    [Column(Order = 20)]
    public required long FileTransferSpeed { get; set; }

    /// <summary>
    /// Gets or sets the total size received of the file in bytes.
    /// </summary>
    [Column(Order = 21)]
    public required long FileDataTransferred { get; set; }

    /// <summary>
    /// Gets or sets the current file transfer bytes offset in combination with the CurrentFileTransferPathIndex used to pause and resume from this offset.
    /// </summary>
    [Column(Order = 22)]
    public long CurrentFileTransferBytesOffset { get; set; }

    [Column(Order = 23)]
    public decimal Percentage { get; set; }

    /// <summary>
    /// Gets the time remaining in seconds the <see cref="DownloadTaskFileBase"/> to finish.
    /// </summary>
    [Column(Order = 24)]
    public required int TimeRemaining { get; set; }

    #endregion

    #region Relationships

    /// <summary>
    /// Gets or sets the destination folder path id of the <see cref="DownloadTaskFileBase"/>.
    /// This allows the user to pick a destination folder for the download, where the path is copied over once downloading begins.
    /// </summary>
    [Column(Order = 25)]
    public required int? DestinationFolderPathId { get; set; }

    #endregion

    #region Helpers

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.None;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.None;

    [NotMapped]
    public string DestinationFilePath => Path.Join(DestinationDirectory, FileName);

    [NotMapped]
    public string DownloadFilePath => Path.Combine(DownloadDirectory, FileName.AddReaparrTempSuffixToFileName());

    [NotMapped]
    public DownloadTaskPhase DownloadTaskPhase => DownloadStatus.ToDownloadTaskPhase();

    [NotMapped]
    public long Speed => DownloadTaskPhaseExtensions.Speed(DownloadTaskPhase, this, this);

    /// <summary>
    /// Gets the download directory appended to the MediaPath e.g: [DownloadPath]/[TvShow]/[Season]/ or  [DownloadPath]/[Movie]/.
    /// </summary>
    [NotMapped]
    public string DownloadDirectory => DirectoryMeta.GetDownloadDirectory(DownloadTaskType);

    /// <summary>
    /// Gets the destination directory appended to the MediaPath e.g: [DestinationPath]/[TvShow]/[Season]/ or  [DestinationPath]/[Movie]/.
    /// </summary>
    [NotMapped]
    public string DestinationDirectory => DirectoryMeta.GetDestinationDirectory(DownloadTaskType);

    public override string ToString() =>
        $"[MoveDownloadFileProgress {Title} - {Percentage:F2}% - {DataFormat.FormatSpeedString(Speed)} - {DataFormat.FormatSizeString(DownloadTaskPhase == DownloadTaskPhase.FileTransfer ? FileDataTransferred : DataReceived)} / {DataFormat.FormatSizeString(DataTotal)} - {DataFormat.FormatTimeSpanString(TimeSpan.FromSeconds(TimeRemaining))}]";

    public IDownloadFileTransferProgress ToFileTransferProgress() =>
        new DownloadFileTransferProgress
        {
            FileTransferSpeed = FileTransferSpeed,
            FileDataTransferred = FileDataTransferred,
            CurrentFileTransferBytesOffset = CurrentFileTransferBytesOffset,
            TimeRemaining = TimeRemaining,
        };

    #endregion
}
