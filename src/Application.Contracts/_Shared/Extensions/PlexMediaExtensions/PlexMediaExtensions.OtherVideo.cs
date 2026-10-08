namespace Reaparr.Application.Contracts;

public static partial class PlexMediaExtensions
{
    public static DownloadTaskOtherVideo MapToDownloadTask(
        this PlexOtherVideo video,
        IntegrationIdentity? integrationIdentity
    ) => new()
    {
        Id = default,
        PlexApiRatingKey = video.PlexApiRatingKey,
        Title = video.Title,
        FullTitle = video.FullTitle,
        Year = video.Year,
        PlexServerId = video.PlexServerId,
        PlexLibraryId = video.PlexLibraryId,
        CreatedAt = DateTime.UtcNow,
        DownloadStatus = DownloadStatus.Queued,
        DataTotal = 0,
        DataReceived = 0,
        DownloadSpeed = 0,
        FileDataTransferred = 0,
        FileTransferSpeed = 0,
        Children = [],
        SonarrIntegrationId = integrationIdentity?.Type == IntegrationType.Sonarr ? integrationIdentity.Id : null,
        RadarrIntegrationId = integrationIdentity?.Type == IntegrationType.Radarr ? integrationIdentity.Id : null,
    };

    public static DownloadTaskOtherVideoFile MapToDownloadTask(
        this PlexOtherVideoMediaData data,
        PlexOtherVideo video,
        CreateDownloadTasksRequest request,
        string downloadRootPath,
        bool keepCompletedInDownloadFolder
    ) => new()
    {
        Id = Guid.Empty,
        Parent = null,
        ParentId = Guid.Empty,
        PlexApiRatingKey = data.PlexApiRatingKey,
        PlexApiMediaId = data.PlexApiMediaId,
        PlexApiPartId = data.PlexApiPartId,
        Title = data.GetFileName,
        FullTitle = $"{video.FullTitle}/{data.GetFileName}",
        PlexServerId = video.PlexServerId,
        PlexLibraryId = video.PlexLibraryId,
        CreatedAt = DateTime.UtcNow,
        DownloadStatus = DownloadStatus.Queued,
        DataTotal = data.Size,
        DataReceived = 0,
        DownloadSpeed = 0,
        FileTransferSpeed = 0,
        FileDataTransferred = 0,
        TimeRemaining = 0,
        HashId = null,
        FileName = data.GetFileName,
        FileLocationUrl = data.Key,
        Quality = data.VideoResolution,
        DirectoryMeta = new DownloadTaskDirectory
        {
            DownloadRootPath = downloadRootPath,
            DestinationRootPath = request.CustomDestinationFolderPath,
            MovieFolder = string.Empty,
            TvShowFolder = string.Empty,
            SeasonFolder = string.Empty,
            MusicArtistFolder = string.Empty,
            MusicAlbumFolder = string.Empty,
            PhotoAlbumFolder = string.Empty,
            OtherVideoFolder = video.Title.SanitizeFolderName(),
            KeepCompletedInDownloadFolder = keepCompletedInDownloadFolder,
        },
        DestinationFolderPathId = request.DestinationFolderPathId,
        DirectDownloadSnapshot = null,
        DownloadClientType = PlexDownloadClientType.Direct,
        SonarrIntegrationId = request.Integration?.Type == IntegrationType.Sonarr ? request.Integration.Id : null,
        RadarrIntegrationId = request.Integration?.Type == IntegrationType.Radarr ? request.Integration.Id : null,
    };
}
