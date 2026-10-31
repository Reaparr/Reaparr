namespace Reaparr.Application.Contracts;

public static partial class PlexMediaExtensions
{
    public static DownloadTaskMovie MapToDownloadTask(
        this PlexMovie plexMovie,
        IntegrationIdentity? integrationIdentity
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = plexMovie.PlexApiRatingKey,
            DataTotal = 0,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = plexMovie.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = plexMovie.PlexLibraryId,
            Title = plexMovie.Title,
            Year = plexMovie.Year,
            FullTitle = plexMovie.FullTitle,
            DataReceived = 0,
            DownloadSpeed = 0,
            FileDataTransferred = 0,
            FileTransferSpeed = 0,
            Children = [],
            SonarrIntegrationId = integrationIdentity?.Type == IntegrationType.Sonarr ? integrationIdentity.Id : null,
            RadarrIntegrationId = integrationIdentity?.Type == IntegrationType.Radarr ? integrationIdentity.Id : null,
        };

    public static DownloadTaskTvShow MapToDownloadTask(
        this PlexTvShow plexTvShow,
        IntegrationIdentity? integrationIdentity
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = plexTvShow.PlexApiRatingKey,
            DataTotal = 0,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = plexTvShow.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = plexTvShow.PlexLibraryId,
            Title = plexTvShow.Title,
            Year = plexTvShow.Year,
            FullTitle = plexTvShow.FullTitle,
            DataReceived = 0,
            DownloadSpeed = 0,
            Children = [],
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            SonarrIntegrationId = integrationIdentity?.Type == IntegrationType.Sonarr ? integrationIdentity.Id : null,
            RadarrIntegrationId = integrationIdentity?.Type == IntegrationType.Radarr ? integrationIdentity.Id : null,
        };

    public static DownloadTaskTvShowSeason MapToDownloadTask(
        this PlexTvShowSeason plexTvShowSeason,
        IntegrationIdentity? integrationIdentity
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = plexTvShowSeason.PlexApiRatingKey,
            DataTotal = 0,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = plexTvShowSeason.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = plexTvShowSeason.PlexLibraryId,
            Title = plexTvShowSeason.Title,
            Year = plexTvShowSeason.Year,
            FullTitle = plexTvShowSeason.FullTitle,
            DataReceived = 0,
            DownloadSpeed = 0,
            Children = [],
            ParentId = default,
            Parent = null,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            SonarrIntegrationId = integrationIdentity?.Type == IntegrationType.Sonarr ? integrationIdentity.Id : null,
            RadarrIntegrationId = integrationIdentity?.Type == IntegrationType.Radarr ? integrationIdentity.Id : null,
        };

    public static DownloadTaskTvShowEpisode MapToDownloadTask(
        this PlexTvShowEpisode plexTvShowEpisode,
        IntegrationIdentity? integrationIdentity
    ) =>
        new()
        {
            Id = default,
            PlexApiRatingKey = plexTvShowEpisode.PlexApiRatingKey,
            DataTotal = 0,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = plexTvShowEpisode.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = plexTvShowEpisode.PlexLibraryId,
            Title = plexTvShowEpisode.Title,
            Year = plexTvShowEpisode.Year,
            FullTitle = plexTvShowEpisode.FullTitle,
            DataReceived = 0,
            DownloadSpeed = 0,
            Children = [],
            ParentId = default,
            Parent = null,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            SonarrIntegrationId = integrationIdentity?.Type == IntegrationType.Sonarr ? integrationIdentity.Id : null,
            RadarrIntegrationId = integrationIdentity?.Type == IntegrationType.Radarr ? integrationIdentity.Id : null,
        };

    public static DownloadTaskMovieFile MapToDownloadTask(
        this PlexMovieMediaData plexMediaData,
        PlexMovie plexMovie,
        CreateDownloadTasksRequest request,
        string downloadRootPath,
        bool keepCompletedInDownloadFolder
    ) =>
        new()
        {
            Id = Guid.Empty,
            PlexApiRatingKey = plexMediaData.PlexApiRatingKey,
            PlexApiMediaId = plexMediaData.PlexApiMediaId,
            PlexApiPartId = plexMediaData.PlexApiPartId,
            HashId = null,
            DataTotal = plexMediaData.Size,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = plexMovie.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = plexMovie.PlexLibraryId,
            DataReceived = 0,
            DownloadSpeed = 0,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            TimeRemaining = 0,
            FileName = plexMediaData.GetFileName,
            FileLocationUrl = plexMediaData.Key,
            VideoQuality = plexMediaData.VideoQuality,
            DirectoryMeta = new DownloadTaskDirectory
            {
                DownloadRootPath = downloadRootPath,
                DestinationRootPath = request.CustomDestinationFolderPath,
                MovieFolder = plexMovie.Title.SanitizeFolderName(),
                TvShowFolder = string.Empty,
                SeasonFolder = string.Empty,
                MusicArtistFolder = string.Empty,
                MusicAlbumFolder = string.Empty,
                PhotoFolder = string.Empty,
                OtherVideoFolder = string.Empty,
                KeepCompletedInDownloadFolder = keepCompletedInDownloadFolder,
            },
            Parent = null,
            ParentId = Guid.Empty,
            DestinationFolderPathId = request.DestinationFolderPathId,
            FullTitle = $"{plexMovie.FullTitle}/{plexMediaData.GetFileName}",
            Title = plexMediaData.GetFileName,
            DirectDownloadSnapshot = null,
            DownloadClientType = PlexDownloadClientType.Direct,
            SonarrIntegrationId = request.Integration?.Type == IntegrationType.Sonarr ? request.Integration.Id : null,
            RadarrIntegrationId = request.Integration?.Type == IntegrationType.Radarr ? request.Integration.Id : null,
        };

    public static DownloadTaskTvShowEpisodeFile MapToDownloadTask(
        this PlexTvShowEpisodeMediaData plexMediaData,
        PlexTvShowEpisode plexTvShowEpisode,
        CreateDownloadTasksRequest request,
        string downloadRootPath,
        bool keepCompletedInDownloadFolder
    )
    {
        if (plexTvShowEpisode.TvShow is null || plexTvShowEpisode.TvShowSeason is null)
        {
            throw new NullReferenceException("PlexTvShowEpisode.TvShow or PlexTvShowEpisode.TvShowSeason is null");
        }

        var fileName = plexMediaData.GetEpisodeDownloadFileName(plexTvShowEpisode.MediaDataList);

        return new DownloadTaskTvShowEpisodeFile
        {
            Id = Guid.Empty,
            PlexApiRatingKey = plexMediaData.PlexApiRatingKey,
            PlexApiMediaId = plexMediaData.PlexApiMediaId,
            PlexApiPartId = plexMediaData.PlexApiPartId,
            HashId = null,
            DataTotal = plexMediaData.Size,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            PlexServer = null,
            PlexServerId = plexTvShowEpisode.PlexServerId,
            PlexLibrary = null,
            PlexLibraryId = plexTvShowEpisode.PlexLibraryId,
            DataReceived = 0,
            DownloadSpeed = 0,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            TimeRemaining = 0,
            FileName = fileName,
            FileLocationUrl = plexMediaData.Key,
            VideoQuality = plexMediaData.VideoQuality,
            DirectoryMeta = new DownloadTaskDirectory
            {
                DownloadRootPath = downloadRootPath,
                DestinationRootPath = request.CustomDestinationFolderPath,
                MovieFolder = string.Empty,
                TvShowFolder = plexTvShowEpisode.TvShow.Title.SanitizeFolderName(),
                SeasonFolder = plexTvShowEpisode.TvShowSeason.Title.SanitizeFolderName(),
                MusicArtistFolder = string.Empty,
                MusicAlbumFolder = string.Empty,
                PhotoFolder = string.Empty,
                OtherVideoFolder = string.Empty,
                KeepCompletedInDownloadFolder = keepCompletedInDownloadFolder,
            },
            Parent = null,
            ParentId = Guid.Empty,
            DestinationFolderPathId = request.DestinationFolderPathId,
            FullTitle = $"{plexTvShowEpisode.FullTitle}/{fileName}",
            Title = fileName,
            DirectDownloadSnapshot = null,
            DownloadClientType = PlexDownloadClientType.Direct,
            SonarrIntegrationId = request.Integration?.Type == IntegrationType.Sonarr ? request.Integration.Id : null,
            RadarrIntegrationId = request.Integration?.Type == IntegrationType.Radarr ? request.Integration.Id : null,
        };
    }

    public static string GetEpisodeDownloadFileName(
        this PlexTvShowEpisodeMediaData mediaData,
        ICollection<PlexTvShowEpisodeMediaData> episodeParts
    )
    {
        var fileName = mediaData.GetFileName;
        if (
            string.IsNullOrEmpty(mediaData.GeneratedFilename)
            || !episodeParts.Any(x =>
                x.PlexApiMediaId == mediaData.PlexApiMediaId
                && x.PlexApiPartId != mediaData.PlexApiPartId
                && string.Equals(x.GetFileName, fileName, StringComparison.OrdinalIgnoreCase)
            )
        )
            return fileName;

        // Part IDs keep generated names stable even when Plex returns parts in a different order.
        return $"{Path.GetFileNameWithoutExtension(fileName)}.part{mediaData.PlexApiPartId}{Path.GetExtension(fileName)}";
    }
}
