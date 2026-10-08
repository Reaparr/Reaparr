namespace Reaparr.Domain;

// TODO find a better method than using a json record for all these folders
public record DownloadTaskDirectory
{
    public required string DownloadRootPath { get; set; }

    public required string DestinationRootPath { get; set; }

    public required string MovieFolder { get; set; }

    public required string TvShowFolder { get; set; }

    public required string SeasonFolder { get; set; }

    public required string MusicArtistFolder { get; set; }

    public required string MusicAlbumFolder { get; set; }

    public required string PhotoAlbumFolder { get; set; }

    public required string OtherVideoFolder { get; set; }

    public required bool KeepCompletedInDownloadFolder { get; set; }

    public string GetDownloadDirectory(DownloadTaskType type)
    {
        if (DownloadRootPath == string.Empty)
            return string.Empty;

        return type switch
        {
            DownloadTaskType.Movie or DownloadTaskType.MovieData or DownloadTaskType.MoviePart => Path.Combine(
                DownloadRootPath,
                "Movies", // TODO use Pathprovider to get the default movie folder for the OS instead of hardcoding "Movies", do for the other media types as well
                MovieFolder
            ),
            DownloadTaskType.TvShow => Path.Combine(DownloadRootPath, "TvShows", TvShowFolder),
            DownloadTaskType.Season
            or DownloadTaskType.Episode
            or DownloadTaskType.EpisodeData
            or DownloadTaskType.EpisodePart => Path.Combine(DownloadRootPath, "TvShows", TvShowFolder, SeasonFolder),
            DownloadTaskType.MusicArtist => Path.Combine(DownloadRootPath, "Music", MusicArtistFolder),
            DownloadTaskType.MusicAlbum
            or DownloadTaskType.MusicTrack
            or DownloadTaskType.MusicTrackData
            or DownloadTaskType.MusicTrackPart => Path.Combine(
                DownloadRootPath,
                "Music",
                MusicArtistFolder,
                MusicAlbumFolder
            ),
            DownloadTaskType.PhotoAlbum
            or DownloadTaskType.PhotoImage
            or DownloadTaskType.PhotoData
            or DownloadTaskType.PhotoPart => Path.Combine(DownloadRootPath, "Photos", PhotoAlbumFolder),
            DownloadTaskType.OtherVideo or DownloadTaskType.OtherVideoData or DownloadTaskType.OtherVideoPart =>
                Path.Combine(DownloadRootPath, "OtherVideos", OtherVideoFolder),
            _ => InvalidDownloadTaskType(type),
        };
    }

    public string GetDestinationDirectory(DownloadTaskType type)
    {
        if (DestinationRootPath == string.Empty)
            return string.Empty;

        return type switch
        {
            DownloadTaskType.Movie or DownloadTaskType.MovieData or DownloadTaskType.MoviePart => Path.Combine(
                DestinationRootPath,
                MovieFolder
            ),
            DownloadTaskType.TvShow => Path.Combine(DestinationRootPath, TvShowFolder),
            DownloadTaskType.Season
            or DownloadTaskType.Episode
            or DownloadTaskType.EpisodeData
            or DownloadTaskType.EpisodePart => Path.Combine(DestinationRootPath, TvShowFolder, SeasonFolder),
            DownloadTaskType.MusicArtist => Path.Combine(DestinationRootPath, MusicArtistFolder),
            DownloadTaskType.MusicAlbum
            or DownloadTaskType.MusicTrack
            or DownloadTaskType.MusicTrackData
            or DownloadTaskType.MusicTrackPart => Path.Combine(
                DestinationRootPath,
                MusicArtistFolder,
                MusicAlbumFolder
            ),
            DownloadTaskType.PhotoAlbum
            or DownloadTaskType.PhotoImage
            or DownloadTaskType.PhotoData
            or DownloadTaskType.PhotoPart => Path.Combine(DestinationRootPath, PhotoAlbumFolder),
            DownloadTaskType.OtherVideo or DownloadTaskType.OtherVideoData or DownloadTaskType.OtherVideoPart =>
                Path.Combine(DestinationRootPath, OtherVideoFolder),
            _ => InvalidDownloadTaskType(type),
        };
    }

    private static string InvalidDownloadTaskType(DownloadTaskType type)
    {
        Result.Fail<string>($"Invalid DownloadTaskType of type: {type}").LogError();
        return string.Empty;
    }
}
