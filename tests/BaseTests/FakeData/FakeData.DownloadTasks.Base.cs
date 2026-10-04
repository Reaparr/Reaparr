namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static Faker<T> ApplyDownloadTaskBase<T>(this Faker<T> faker, DownloadTaskType downloadTaskType)
        where T : DownloadTaskBase
    {
        return faker
            .StrictMode(true)
            .Ignore(x => x.Id)
            .RuleFor(x => x.PlexApiRatingKey, _ => GetUniqueNumber())
            .RuleFor(x => x.Title, f => f.PlexMedia().MediaTitle(downloadTaskType))
            .RuleFor(x => x.FullTitle, (_, x) => x.Title)
            .RuleFor(x => x.DownloadStatus, _ => DownloadStatus.Queued)
            .RuleFor(x => x.CreatedAt, _ => DateTime.UtcNow)
            .Ignore(x => x.PlexServerId)
            .Ignore(x => x.PlexServer)
            .Ignore(x => x.PlexLibraryId)
            .Ignore(x => x.PlexLibrary)
            .Ignore(x => x.SonarrIntegrationId)
            .Ignore(x => x.SonarrIntegration)
            .Ignore(x => x.RadarrIntegrationId)
            .Ignore(x => x.RadarrIntegration);
    }

    private static Faker<T> ApplyDownloadTaskParentBase<T>(this Faker<T> faker, DownloadTaskType downloadTaskType)
        where T : DownloadTaskParentBase
    {
        return faker
            .ApplyDownloadTaskBase(downloadTaskType)
            .RuleFor(x => x.Year, f => f.Random.Int(1900, 2030))
            .Ignore(x => x.FileTransferSpeed)
            .Ignore(x => x.DataReceived)
            .Ignore(x => x.Percentage)
            .Ignore(x => x.TimeRemaining)
            .Ignore(x => x.FileDataTransferred)
            .Ignore(x => x.DownloadSpeed);
    }

    private static Faker<T> ApplyDownloadTaskFileBase<T>(this Faker<T> faker, DownloadTaskType downloadTaskType)
        where T : DownloadTaskFileBase
    {
        return faker
            .ApplyDownloadTaskBase(downloadTaskType)
            .Ignore(x => x.HashId)
            .Ignore(x => x.DataReceived)
            .Ignore(x => x.Percentage)
            .Ignore(x => x.TimeRemaining)
            .Ignore(x => x.DownloadSpeed)
            .Ignore(x => x.FileTransferSpeed)
            .Ignore(x => x.FileDataTransferred)
            .Ignore(x => x.CurrentFileTransferBytesOffset)
            .Ignore(x => x.DestinationFolderPathId)
            .RuleFor(x => x.PlexApiMediaId, _ => GetUniqueNumber())
            .RuleFor(x => x.PlexApiPartId, _ => GetUniqueNumber())
            .RuleFor(
                x => x.Quality,
                f => f.PickRandom(VideoQuality.SD, VideoQuality.HD, VideoQuality.FullHD, VideoQuality.UHD_4K)
            )
            .RuleFor(
                x => x.FileName,
                (_, x) =>
                    $"{x.MediaType.ToPlexMediaTypeString()}-{x.Title.SanitizeFolderName()}.[{x.Quality.ToResolutionLabel()}].file.mp4"
            )
            .RuleFor(x => x.FileLocationUrl, _ => DownloadFileUrl)
            .RuleFor(
                x => x.DirectoryMeta,
                (_, x) =>
                    new DownloadTaskDirectory
                    {
                        DestinationRootPath = x.MediaType.ToDefaultDestinationLocation(),
                        DownloadRootPath = PlexMediaType.None.ToDefaultDestinationLocation(),
                        MovieFolder = x.Title,
                        TvShowFolder = string.Empty,
                        SeasonFolder = string.Empty,
                        KeepCompletedInDownloadFolder = false,
                    }
            )
            .RuleFor(x => x.DirectDownloadSnapshot, _ => null)
            .RuleFor(x => x.DownloadClientType, _ => PlexDownloadClientType.Direct);
    }

    private static string ToDefaultDestinationLocation(this PlexMediaType type)
    {
        return type switch
        {
            PlexMediaType.None => "/Downloads",
            PlexMediaType.Movie => "/Movies",
            PlexMediaType.TvShow => "/TvShows",
            PlexMediaType.Season => "/TvShows",
            PlexMediaType.Episode => "/TvShows",
            PlexMediaType.Music => "/Music",
            PlexMediaType.Album => "/Music",
            PlexMediaType.Track => "/Music",
            PlexMediaType.Photos => "/Photos",
            PlexMediaType.OtherVideos => "/Videos",
            PlexMediaType.Games => "/Games",
            _ => "/Downloads",
        };
    }
}
