namespace Reaparr.Domain;

public static partial class TelemetryProperty
{
    /// <summary>
    /// Groups download event keys, fixed source labels, and trace-tag names. Used by the telemetry
    /// facade to describe task creation, execution, file movement, and cleanup.
    /// </summary>
    public static class Downloads
    {
        public const string TaskType = "task_type";
        public const string Client = "client";
        public const string Source = "source";
        public const string PlexSource = "plex";
        public const string ConfiguredSegments = "configured_segments";
        public const string Segments = "segments";
        public const string Attempt = "attempt";
        public const string RetryCount = "retry_count";
        public const string DataTotal = "data_total";
        public const string DownloadPath = "download_path";
        public const string DestinationPath = "destination_path";
        public const string SourcePath = "source_path";
        public const string NormalizedFileName = "normalized_file_name";
        public const string OutputPath = "output_path";
        public const string KeepInDownloads = "keep_in_downloads";
        public const string RetentionReason = "retention_reason";
        public const string EffectiveSpeedLimitBytes = "effective_speed_limit_bytes";
        public const string SelectedCount = "selected_count";
        public const string SelectionCount = "selection_count";
        public const string SelectedMediaTypes = "selected_media_types";
        public const string DestinationFolderPathId = "destination_folder_path_id";
        public const string CustomDestinationFolderPath = "custom_destination_folder_path";
        public const string PreviewCount = "preview_count";
        public const string PreviewTotalSize = "preview_total_size";
        public const string PreviewTotalChildren = "preview_total_children";
        public const string Report = "report";
        public const string ReportId = "id";
        public const string ReportTitle = "title";
        public const string ReportSize = "size";
        public const string ReportChildCount = "child_count";
        public const string ReportMediaType = "media_type";
        public const string CreatedTotal = "created_total";
        public const string RootTaskIds = "root_task_ids";
        public const string CompletionElapsedMilliseconds = "completion_elapsed_ms";
        public const string CreatedReport = "created_report";
        public const string Movies = "movies";
        public const string TvShows = "tv_shows";
        public const string Seasons = "seasons";
        public const string Episodes = "episodes";
        public const string PhotoFolders = "photo_folders";
        public const string PhotoImages = "photo_images";
        public const string MusicArtists = "music_artists";
        public const string MusicAlbums = "music_albums";
        public const string MusicTracks = "music_tracks";
        public const string OtherVideos = "other_videos";
        public const string DownloadFileNameTag = "download.fileName";
        public const string DownloadSourcePathTag = "download.sourcePath";
        public const string DownloadPathTag = "download.downloadPath";
        public const string DestinationPathTag = "download.destinationPath";
        public const string SegmentsTag = "download.segments";
        public const string ClientTag = "download.client";
        public const string SourceTag = "download.source";
        public const string OutputQualityTag = "download.outputQuality";
        public const string NormalizedFileNameTag = "download.normalizedFileName";
        public const string CleanupDirectoryTag = "download.directory";
        public const string OutcomeTag = "outcome";
    }
}
