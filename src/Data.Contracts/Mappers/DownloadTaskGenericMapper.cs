namespace Reaparr.Data.Contracts;

public static class DownloadTaskGenericMapper
{
    #region Parent

    public static DownloadTaskGeneric ToGeneric(this DownloadTaskParentBase task)
    {
        var children = task switch
        {
            DownloadTaskMovie x => x.Children.Select(child => child.ToGeneric()).ToList(),
            DownloadTaskTvShow x => x.Children.Select(child => child.ToGeneric()).ToList(),
            DownloadTaskTvShowSeason x => x.Children.Select(child => child.ToGeneric()).ToList(),
            DownloadTaskTvShowEpisode x => x.Children.Select(child => child.ToGeneric()).ToList(),
            DownloadTaskPhotoAlbum x => x.Children.Select(child => child.ToGeneric()).ToList(),
            DownloadTaskPhotoImage x => x.Children.Select(child => child.ToGeneric()).ToList(),
            DownloadTaskMusicArtist x => x.Children.Select(child => child.ToGeneric()).ToList(),
            DownloadTaskMusicAlbum x => x.Children.Select(child => child.ToGeneric()).ToList(),
            DownloadTaskMusicTrack x => x.Children.Select(child => child.ToGeneric()).ToList(),
            DownloadTaskOtherVideo x => x.Children.Select(child => child.ToGeneric()).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(task)),
        };
        var (downloadDirectory, destinationDirectory) = task.GetDirectories();
        var generic = new DownloadTaskGeneric
        {
            Id = task.Id,
            RatingKey = task.PlexApiRatingKey,
            Title = task.Title,
            FullTitle = task.FullTitle,
            MediaType = task.MediaType,
            DownloadTaskType = task.DownloadTaskType,
            DownloadStatus = task.DownloadStatus,
            Percentage = task.Percentage,
            DataReceived = task.DataReceived,
            DataTotal = task.DataTotal,
            TimeRemaining = task.TimeRemaining,
            CreatedAt = task.CreatedAt,
            FileName = string.Empty,
            IsDownloadable = task.IsDownloadable,
            DownloadDirectory = downloadDirectory,
            DestinationDirectory = destinationDirectory,
            Quality = VideoQuality.None,
            FileLocationUrl = string.Empty,
            DownloadSpeed = task.DownloadSpeed,
            FileTransferSpeed = task.FileTransferSpeed,
            FileDataTransferred = task.FileDataTransferred,
            CurrentFileTransferBytesOffset = 0,
            Children = children,
            ParentId = task.ToParentKey()?.Id ?? Guid.Empty,
            PlexServer = task.PlexServer,
            PlexServerId = task.PlexServerId,
            PlexLibrary = task.PlexLibrary,
            PlexLibraryId = task.PlexLibraryId,
        };
        generic.Calculate();
        return generic;
    }

    #endregion

    #region File

    public static DownloadTaskGeneric ToGeneric(this DownloadTaskFileBase file)
    {
        var phase = file.DownloadStatus.ToDownloadTaskPhase();

        var downloadTaskGeneric = new DownloadTaskGeneric
        {
            Id = file.Id,
            RatingKey = file.PlexApiRatingKey,
            Title = file.Title,
            FullTitle = file.FullTitle,
            MediaType = file.MediaType,
            DownloadTaskType = file.DownloadTaskType,
            DownloadStatus = file.DownloadStatus,
            Percentage = DownloadTaskPhaseExtensions.Percentage(phase, file, file),
            DataReceived = file.DataReceived,
            DataTotal = file.DataTotal,
            TimeRemaining =
                phase == DownloadTaskPhase.FileTransfer || phase == DownloadTaskPhase.Completed
                    ? DownloadTaskPhaseExtensions.TimeRemaining(phase, file, file)
                    : file.TimeRemaining,
            CreatedAt = file.CreatedAt,
            FileName = file.FileName,
            IsDownloadable = file.IsDownloadable,
            DownloadDirectory = file.DownloadDirectory,
            DestinationDirectory = file.DestinationDirectory,
            FileLocationUrl = file.FileLocationUrl,
            DownloadSpeed = file.DownloadSpeed,
            FileTransferSpeed = file.FileTransferSpeed,
            Children = [],
            Quality = file is DownloadTaskTvShowEpisodeFile ? VideoQuality.None : file.Quality,
            ParentId = file.ToParentKey()!.Id,
            PlexServer = file.PlexServer,
            PlexServerId = file.PlexServerId,
            PlexLibrary = file.PlexLibrary,
            PlexLibraryId = file.PlexLibraryId,
            FileDataTransferred = file.FileDataTransferred,
            CurrentFileTransferBytesOffset = file.CurrentFileTransferBytesOffset,
        };
        return downloadTaskGeneric;
    }

    #endregion
}
