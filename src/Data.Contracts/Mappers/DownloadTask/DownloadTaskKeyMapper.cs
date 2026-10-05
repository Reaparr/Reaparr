namespace Reaparr.Data.Contracts;

public static class DownloadTaskKeyMapper
{
    #region PlexMovie

    public static IQueryable<DownloadTaskKey> ProjectToKey(this IQueryable<DownloadTaskMovie> downloadTaskMovie) =>
        downloadTaskMovie.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = x.DownloadTaskType,
        });

    #endregion

    #region PlexMovieFile

    public static IQueryable<DownloadTaskKey> ProjectToKey(
        this IQueryable<DownloadTaskMovieFile> downloadTaskMovieFile
    ) =>
        downloadTaskMovieFile.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = x.DownloadTaskType,
        });

    public static IQueryable<DownloadTaskKey> ProjectToParentKey(
        this IQueryable<DownloadTaskMovieFile> downloadTaskMovieFile
    ) =>
        downloadTaskMovieFile.Select(x => new DownloadTaskKey
        {
            Id = x.ParentId,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.Movie,
        });

    #endregion

    #region PlexTvShow

    public static IQueryable<DownloadTaskKey> ProjectToKey(this IQueryable<DownloadTaskTvShow> downloadTaskTvShow) =>
        downloadTaskTvShow.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = x.DownloadTaskType,
        });

    #endregion

    #region PlexSeason

    public static IQueryable<DownloadTaskKey> ProjectToKey(
        this IQueryable<DownloadTaskTvShowSeason> downloadTaskTvShowSeason
    ) =>
        downloadTaskTvShowSeason.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = x.DownloadTaskType,
        });

    #endregion

    #region PlexTvShowEpisode

    public static IQueryable<DownloadTaskKey> ProjectToKey(
        this IQueryable<DownloadTaskTvShowEpisode> downloadTaskTvShowEpisode
    ) =>
        downloadTaskTvShowEpisode.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = x.DownloadTaskType,
        });

    #endregion

    #region PlexTvShowEpisodeFile

    public static IQueryable<DownloadTaskKey> ProjectToKey(
        this IQueryable<DownloadTaskTvShowEpisodeFile> downloadTaskTvShowEpisodeFile
    ) =>
        downloadTaskTvShowEpisodeFile.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = x.DownloadTaskType,
        });

    public static IQueryable<DownloadTaskKey> ProjectToParentKey(
        this IQueryable<DownloadTaskTvShowEpisodeFile> downloadTaskTvShowEpisodeFile
    ) =>
        downloadTaskTvShowEpisodeFile.Select(x => new DownloadTaskKey
        {
            Id = x.ParentId,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.Episode,
        });

    #endregion
    public static IQueryable<DownloadTaskKey> ProjectToKey<T>(this IQueryable<T> tasks)
        where T : DownloadTaskBase =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = x.DownloadTaskType,
        });

    public static IQueryable<DownloadTaskKey> ProjectToParentKey(this IQueryable<DownloadTaskMusicAlbum> tasks) =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.ParentId,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.MusicArtist,
        });

    public static IQueryable<DownloadTaskKey> ProjectToParentKey(this IQueryable<DownloadTaskMusicTrack> tasks) =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.ParentId,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.MusicAlbum,
        });

    public static IQueryable<DownloadTaskKey> ProjectToParentKey(this IQueryable<DownloadTaskMusicTrackFile> tasks) =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.ParentId,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.MusicTrack,
        });

    public static IQueryable<DownloadTaskKey> ProjectToParentKey(this IQueryable<DownloadTaskOtherVideoFile> tasks) =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.ParentId,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.OtherVideo,
        });

    public static IQueryable<DownloadTaskKey> ProjectToKey(this IQueryable<DownloadTaskPhotoAlbum> tasks) =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.PhotoAlbum,
        });

    public static IQueryable<DownloadTaskKey> ProjectToKey(this IQueryable<DownloadTaskPhotoImage> tasks) =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.PhotoImage,
        });

    public static IQueryable<DownloadTaskKey> ProjectToKey(this IQueryable<DownloadTaskPhotoImageFile> tasks) =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.Id,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.PhotoData,
        });

    public static IQueryable<DownloadTaskKey> ProjectToParentKey(this IQueryable<DownloadTaskPhotoImage> tasks) =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.ParentId,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.PhotoAlbum,
        });

    public static IQueryable<DownloadTaskKey> ProjectToParentKey(this IQueryable<DownloadTaskPhotoImageFile> tasks) =>
        tasks.Select(x => new DownloadTaskKey
        {
            Id = x.ParentId,
            PlexServerId = x.PlexServerId,
            PlexLibraryId = x.PlexLibraryId,
            Type = DownloadTaskType.PhotoImage,
        });
}
