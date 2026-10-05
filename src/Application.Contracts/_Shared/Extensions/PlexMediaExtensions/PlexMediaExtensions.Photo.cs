namespace Reaparr.Application.Contracts;

public static partial class PlexMediaExtensions
{
    public static DownloadTaskPhotoAlbum MapToDownloadTask(
        this PlexPhotoAlbum photoAlbum,
        IntegrationIdentity? integrationIdentity
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = photoAlbum.PlexApiRatingKey,
            DataTotal = 0,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = photoAlbum.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = photoAlbum.PlexLibraryId,
            Title = photoAlbum.Title,
            Year = photoAlbum.Year,
            FullTitle = photoAlbum.FullTitle,
            DataReceived = 0,
            DownloadSpeed = 0,
            FileDataTransferred = 0,
            FileTransferSpeed = 0,
            Children = [],
            SonarrIntegrationId = integrationIdentity?.Type == IntegrationType.Sonarr ? integrationIdentity.Id : null,
            RadarrIntegrationId = integrationIdentity?.Type == IntegrationType.Radarr ? integrationIdentity.Id : null,
        };

    public static DownloadTaskPhotoImage MapToDownloadTask(
        this PlexPhotoImage photoImage,
        DownloadTaskPhotoAlbum parent,
        IntegrationIdentity? integrationIdentity
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = photoImage.PlexApiRatingKey,
            DataTotal = 0,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = photoImage.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = photoImage.PlexLibraryId,
            Title = photoImage.Title,
            Year = photoImage.Year,
            FullTitle = photoImage.FullTitle,
            DataReceived = 0,
            DownloadSpeed = 0,
            FileDataTransferred = 0,
            FileTransferSpeed = 0,
            Children = [],
            Parent = parent,
            ParentId = parent.Id,
            SonarrIntegrationId = integrationIdentity?.Type == IntegrationType.Sonarr ? integrationIdentity.Id : null,
            RadarrIntegrationId = integrationIdentity?.Type == IntegrationType.Radarr ? integrationIdentity.Id : null,
        };

    public static DownloadTaskPhotoImageFile MapToDownloadTask(
        this PlexPhotoMediaData mediaData,
        DownloadTaskPhotoImage parent,
        PlexPhotoImage photoImage,
        PlexPhotoAlbum photoAlbum,
        CreateDownloadTasksRequest request,
        string downloadRootPath,
        bool keepCompletedInDownloadFolder
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = mediaData.PlexApiRatingKey,
            PlexApiMediaId = mediaData.PlexApiMediaId,
            PlexApiPartId = mediaData.PlexApiPartId,
            HashId = null,
            DataTotal = mediaData.Size,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = photoImage.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = photoImage.PlexLibraryId,
            DataReceived = 0,
            DownloadSpeed = 0,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            TimeRemaining = 0,
            FileName = mediaData.OriginalFilename.GetFileName(),
            FileLocationUrl = mediaData.Key,
            Quality = mediaData.VideoResolution,
            DirectoryMeta = new DownloadTaskDirectory
            {
                DownloadRootPath = downloadRootPath,
                DestinationRootPath = request.CustomDestinationFolderPath,
                MovieFolder = string.Empty,
                TvShowFolder = string.Empty,
                SeasonFolder = string.Empty,
                PhotoAlbumFolder = photoAlbum.Title.SanitizeFolderName(),
                KeepCompletedInDownloadFolder = keepCompletedInDownloadFolder,
            },
            Parent = parent,
            ParentId = parent.Id,
            DestinationFolderPathId = request.DestinationFolderPathId,
            FullTitle = $"{photoImage.FullTitle}/{mediaData.OriginalFilename.GetFileName()}",
            Title = mediaData.OriginalFilename.GetFileName(),
            DirectDownloadSnapshot = null,
            DownloadClientType = PlexDownloadClientType.Direct,
            SonarrIntegrationId = request.Integration?.Type == IntegrationType.Sonarr ? request.Integration.Id : null,
            RadarrIntegrationId = request.Integration?.Type == IntegrationType.Radarr ? request.Integration.Id : null,
        };
}
