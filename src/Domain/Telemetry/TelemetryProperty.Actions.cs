namespace Reaparr.Domain;

public static partial class TelemetryProperty
{
    /// <summary>
    /// Groups keys for authentication, folder browsing, notifications, and application updates.
    /// Used by the telemetry facade to attach feature-specific context to action events.
    /// </summary>
    public static class Actions
    {
        public const string RememberMe = "remember_me";
        public const string UsernameChanged = "username_changed";
        public const string PasswordChanged = "password_changed";
        public const string DirectoryCount = "directory_count";
        public const string FileCount = "file_count";
        public const string RootListing = "root_listing";
        public const string FolderType = "folder_type";
        public const string NotificationId = "notification_id";
        public const string Hidden = "hidden";
        public const string PackageId = "package_id";
        public const string PendingRestart = "pending_restart";
    }
}
