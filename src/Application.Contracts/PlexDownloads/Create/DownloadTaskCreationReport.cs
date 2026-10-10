namespace Reaparr.Application.Contracts;

/// <summary>
/// Reports counts of download tasks created, broken down by media type.
/// </summary>
public sealed record DownloadTaskCreationReport
{
    public int Movies { get; init; }
    public int TvShows { get; init; }
    public int Seasons { get; init; }
    public int Episodes { get; init; }
    public int PhotoAlbums { get; init; }
    public int PhotoImages { get; init; }
    public int MusicArtists { get; init; }
    public int MusicAlbums { get; init; }
    public int MusicTracks { get; init; }
    public int OtherVideos { get; init; }

    public int Total => Movies + TvShows + Seasons + Episodes + PhotoAlbums + PhotoImages
        + MusicArtists + MusicAlbums + MusicTracks + OtherVideos;

    public static DownloadTaskCreationReport operator +(
        DownloadTaskCreationReport left,
        DownloadTaskCreationReport right
    ) =>
        new()
        {
            Movies = left.Movies + right.Movies,
            TvShows = left.TvShows + right.TvShows,
            Seasons = left.Seasons + right.Seasons,
            Episodes = left.Episodes + right.Episodes,
            PhotoAlbums = left.PhotoAlbums + right.PhotoAlbums,
            PhotoImages = left.PhotoImages + right.PhotoImages,
            MusicArtists = left.MusicArtists + right.MusicArtists,
            MusicAlbums = left.MusicAlbums + right.MusicAlbums,
            MusicTracks = left.MusicTracks + right.MusicTracks,
            OtherVideos = left.OtherVideos + right.OtherVideos,
        };
}
