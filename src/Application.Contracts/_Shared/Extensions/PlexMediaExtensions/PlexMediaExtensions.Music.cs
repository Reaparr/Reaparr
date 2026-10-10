namespace Reaparr.Application.Contracts;

public static partial class PlexMediaExtensions
{
    public static DownloadTaskMusicArtist MapToDownloadTask(
        this PlexMusicArtist artist,
        IntegrationIdentity? integrationIdentity
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = artist.PlexApiRatingKey,
            DataTotal = 0,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = artist.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = artist.PlexLibraryId,
            Title = artist.Title,
            Year = artist.Year,
            FullTitle = artist.FullTitle,
            DataReceived = 0,
            DownloadSpeed = 0,
            FileDataTransferred = 0,
            FileTransferSpeed = 0,
            Children = [],
            SonarrIntegrationId = integrationIdentity?.Type == IntegrationType.Sonarr ? integrationIdentity.Id : null,
            RadarrIntegrationId = integrationIdentity?.Type == IntegrationType.Radarr ? integrationIdentity.Id : null,
        };

    public static DownloadTaskMusicAlbum MapToDownloadTask(
        this PlexMusicAlbum album,
        DownloadTaskMusicArtist parent,
        IntegrationIdentity? integrationIdentity
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = album.PlexApiRatingKey,
            DataTotal = 0,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = album.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = album.PlexLibraryId,
            Title = album.Title,
            Year = album.Year,
            FullTitle = album.FullTitle,
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

    public static DownloadTaskMusicTrack MapToDownloadTask(
        this PlexMusicTrack track,
        DownloadTaskMusicAlbum parent,
        IntegrationIdentity? integrationIdentity
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = track.PlexApiRatingKey,
            DataTotal = 0,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = track.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = track.PlexLibraryId,
            Title = track.Title,
            Year = track.Year,
            FullTitle = track.FullTitle,
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

    public static DownloadTaskMusicTrackFile MapToDownloadTask(
        this PlexMusicTrackMediaData mediaData,
        DownloadTaskMusicTrack parent,
        PlexMusicTrack track,
        PlexMusicAlbum album,
        PlexMusicArtist artist,
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
            PlexServerId = track.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = track.PlexLibraryId,
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
                MusicArtistFolder = artist.Title.SanitizeFolderName(),
                MusicAlbumFolder = album.Title.SanitizeFolderName(),
                PhotoAlbumFolder = string.Empty,
                OtherVideoFolder = string.Empty,
                KeepCompletedInDownloadFolder = keepCompletedInDownloadFolder,
            },
            Parent = parent,
            ParentId = parent.Id,
            DestinationFolderPathId = request.DestinationFolderPathId,
            FullTitle = $"{track.FullTitle}/{mediaData.OriginalFilename.GetFileName()}",
            Title = mediaData.OriginalFilename.GetFileName(),
            DirectDownloadSnapshot = null,
            DownloadClientType = PlexDownloadClientType.Direct,
            SonarrIntegrationId = null,
            RadarrIntegrationId = null,
        };
}
