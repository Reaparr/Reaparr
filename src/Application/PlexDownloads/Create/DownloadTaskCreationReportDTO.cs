namespace Reaparr.Application;

/// <summary>
/// Reports counts of download tasks created, broken down by media type.
/// </summary>
public sealed record DownloadTaskCreationReportDTO
{
    public required int Movies { get; init; }
    public required int TvShows { get; init; }
    public required int Seasons { get; init; }
    public required int Episodes { get; init; }
    public required int PhotoAlbums { get; init; }
    public required int PhotoImages { get; init; }
    public required int MusicArtists { get; init; }
    public required int MusicAlbums { get; init; }
    public required int MusicTracks { get; init; }
    public required int OtherVideos { get; init; }

    public int Total => Movies + TvShows + Seasons + Episodes + PhotoAlbums + PhotoImages
        + MusicArtists + MusicAlbums + MusicTracks + OtherVideos;
}
