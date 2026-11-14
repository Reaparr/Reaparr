namespace Reaparr.Domain;

public static partial class TelemetryProperty
{
    /// <summary>
    /// Groups keys for integration lifecycle, indexing, and external download operations. Used by
    /// the telemetry facade to report query context, task-creation summaries, and cleanup results.
    /// </summary>
    public static class Integrations
    {
        public const string ConnectionIdentityChanged = "connection_identity_changed";
        public const string RemoteCleanupOutcome = "remote_cleanup_outcome";
        public const string LocalRemoved = "local_removed";
        public const string Forced = "forced";
        public const string QueryType = "query_type";
        public const string ImdbId = "imdb_id";
        public const string TmdbId = "tmdb_id";
        public const string TvdbId = "tvdb_id";
        public const string Season = "season";
        public const string Episode = "episode";
        public const string Categories = "categories";
        public const string VideoQuality = "video_quality";
        public const string PieceLength = "piece_length";
        public const string CreatedMovies = "movies";
        public const string CreatedTvShows = "tv_shows";
        public const string CreatedSeasons = "seasons";
        public const string CreatedEpisodes = "episodes";
        public const string CreatedPhotoFolders = "photo_folders";
        public const string CreatedPhotoImages = "photo_images";
        public const string CreatedMusicArtists = "music_artists";
        public const string CreatedMusicAlbums = "music_albums";
        public const string CreatedMusicTracks = "music_tracks";
        public const string CreatedOtherVideos = "other_videos";
        public const string PieceCount = "piece_count";
        public const string AssociatedTaskCount = "associated_task_count";
        public const string CreatedTotal = "created_total";
        public const string CreatedReport = "created_report";
        public const string RequestedHashCount = "requested_hash_count";
        public const string AllRequested = "all_requested";
        public const string ActiveCount = "active_count";
        public const string NonActiveCount = "non_active_count";
        public const string EligibleCount = "eligible_count";
        public const string StopFailureCount = "stop_failure_count";
        public const string StopCancellationCount = "stop_cancellation_count";
        public const string FileFailureCount = "file_failure_count";
        public const string FileCancellationCount = "file_cancellation_count";
        public const string CleanupFailureCount = "cleanup_failure_count";
    }
}
