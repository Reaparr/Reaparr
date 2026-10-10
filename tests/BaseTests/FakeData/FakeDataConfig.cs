namespace Reaparr.BaseTests;

public class FakeDataConfig : BaseConfig<FakeDataConfig>
{
    public int PlexServerCount { get; set; } = 1;

    public int PlexServerConnectionPerServerCount { get; set; } = 4;

    public int PlexMovieLibraryCount { get; set; } = 0;
    public int PlexTvShowLibraryCount { get; set; } = 0;
    public int PlexMusicLibraryCount { get; set; } = 0;
    public int PlexPhotoLibraryCount { get; set; } = 0;
    public int PlexOtherVideoLibraryCount { get; set; } = 0;

    /// <summary>
    /// The number of PlexAccounts to create which will have access to every PlexServer and PlexLibrary by default.
    /// </summary>
    public int PlexAccountCount { get; set; } = 0;

    public int RadarrIntegrationCount { get; set; } = 0;

    public int SonarrIntegrationCount { get; set; } = 0;

    public bool AssignUnownedDownloadTasksToRadarrIntegration { get; set; }

    #region Movie

    public int MovieCount { get; set; } = 0;

    #endregion

    #region TvShow

    public int TvShowCount { get; set; } = 0;

    public int TvShowSeasonCount { get; set; } = 0;

    public int TvShowEpisodeCount { get; set; } = 0;

    #endregion

    #region Music

    /// <summary>Artists per music library.</summary>
    public int MusicArtistCount { get; set; } = 0;

    /// <summary>Albums per artist.</summary>
    public int MusicAlbumCount { get; set; } = 0;

    /// <summary>Tracks per album.</summary>
    public int MusicTrackCount { get; set; } = 0;

    #endregion

    #region Photos

    /// <summary>Albums per photo library.</summary>
    public int PhotoAlbumCount { get; set; } = 0;

    /// <summary>Still photos per album, excluding video clips.</summary>
    public int PhotoCount { get; set; } = 0;

    /// <summary>Additional video clips per photo album.</summary>
    public int PhotoClipCount { get; set; } = 0;

    #endregion

    #region OtherVideos

    /// <summary>Videos per Other Videos library.</summary>
    public int OtherVideoCount { get; set; } = 0;

    #endregion

    #region DownloadTasks

    public int MovieDownloadTasksCount { get; set; } = 0;

    public int TvShowDownloadTasksCount { get; set; } = 0;

    public int TvShowSeasonDownloadTasksCount { get; set; } = 0;

    public int TvShowEpisodeDownloadTasksCount { get; set; } = 0;

    public int MusicArtistDownloadTasksCount { get; set; } = 0;

    public int MusicAlbumDownloadTasksCount { get; set; } = 0;

    public int MusicTrackDownloadTasksCount { get; set; } = 0;

    public int MusicTrackFileDownloadTasksCount { get; set; } = 0;

    public int PhotoAlbumDownloadTasksCount { get; set; } = 0;

    public int PhotoImageDownloadTasksCount { get; set; } = 0;

    public int PhotoImageFileDownloadTasksCount { get; set; } = 0;

    public int OtherVideoDownloadTasksCount { get; set; } = 0;

    public int OtherVideoFileDownloadTasksCount { get; set; } = 0;

    #endregion

    public bool IncludeMultiPartMovies { get; set; }

    public bool IncludeMultiPartEpisodes { get; set; }

    public bool AccountHasAccessToAllLibraries { get; set; }

    public int DownloadFileSizeInMb { get; set; } = 10;

    public bool ShouldHavePlexServer => PlexServerCount > 0 || ShouldHavePlexLibrary;

    public bool ShouldHavePlexLibrary =>
        ShouldHaveMoviePlexLibrary
        || ShouldHaveTvShowPlexLibrary
        || ShouldHaveMusicPlexLibrary
        || ShouldHavePhotoPlexLibrary
        || ShouldHaveOtherVideoPlexLibrary;

    public bool ShouldHaveMoviePlexLibrary =>
        PlexMovieLibraryCount > 0 || MovieCount > 0 || MovieDownloadTasksCount > 0;

    public bool ShouldHaveTvShowPlexLibrary =>
        PlexTvShowLibraryCount > 0
        || TvShowCount > 0
        || TvShowSeasonCount > 0
        || TvShowEpisodeCount > 0
        || TvShowDownloadTasksCount > 0
        || TvShowSeasonDownloadTasksCount > 0
        || TvShowEpisodeDownloadTasksCount > 0;

    public bool ShouldHaveMusicPlexLibrary =>
        PlexMusicLibraryCount > 0
        || MusicArtistCount > 0
        || MusicAlbumCount > 0
        || MusicTrackCount > 0
        || MusicArtistDownloadTasksCount > 0
        || MusicAlbumDownloadTasksCount > 0
        || MusicTrackDownloadTasksCount > 0
        || MusicTrackFileDownloadTasksCount > 0;

    public bool ShouldHavePhotoPlexLibrary =>
        PlexPhotoLibraryCount > 0
        || PhotoAlbumCount > 0
        || PhotoCount > 0
        || PhotoClipCount > 0
        || PhotoAlbumDownloadTasksCount > 0
        || PhotoImageDownloadTasksCount > 0
        || PhotoImageFileDownloadTasksCount > 0;

    public bool ShouldHaveOtherVideoPlexLibrary =>
        PlexOtherVideoLibraryCount > 0
        || OtherVideoCount > 0
        || OtherVideoDownloadTasksCount > 0
        || OtherVideoFileDownloadTasksCount > 0;
}
