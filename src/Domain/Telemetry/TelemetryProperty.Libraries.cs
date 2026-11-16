namespace Reaparr.Domain;

public static partial class TelemetryProperty
{
    /// <summary>
    /// Groups keys for media browsing, library configuration, synchronization, and comparison.
    /// Used by the telemetry facade to describe the scope and results of library operations.
    /// </summary>
    public static class Libraries
    {
        public const string Search = "search";
        public const string Filter = "filter";
        public const string Sort = "sort";
        public const string Direction = "direction";
        public const string QueueStatus = "queue_status";
        public const string ForceLibrarySync = "force_library_sync";
        public const string ForceMediaRefresh = "force_media_refresh";
        public const string TotalMediaSize = "total_media_size";
        public const string MediaCount = "media_count";
        public const string Offline = "offline";
        public const string Pair = "pair";
        public const string OwnedLibraryId = "owned_library_id";
        public const string RemoteLibraryId = "remote_library_id";
        public const string IsEnabled = "is_enabled";
        public const string PurgedMediaCount = "purged_media_count";
        public const string DefaultDestinationId = "default_destination_id";
        public const string DefaultDestination = "default_destination";
        public const string Access = "access";
        public const string Granted = "granted";
        public const string Revoked = "revoked";
        public const string ProcessedCount = "processed_count";
        public const string Interrupted = "interrupted";
        public const string ItemCount = "item_count";
        public const string FolderType = "folder_type";
        public const string ServerName = "server_name";
        public const string LibraryName = "library_name";
        public const string State = "state";
    }
}
