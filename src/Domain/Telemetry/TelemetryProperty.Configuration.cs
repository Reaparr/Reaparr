namespace Reaparr.Domain;

public static partial class TelemetryProperty
{
    /// <summary>
    /// Groups fields exposed in external integration configuration payloads. Used by the telemetry
    /// facade when reporting Sonarr and Radarr configuration and lifecycle changes.
    /// </summary>
    public static class Configuration
    {
        public const string Name = "name";
        public const string Url = "url";
        public const string Category = "category";
        public const string DownloadFolderId = "downloadFolderId";
        public const string ProvisioningState = "provisioningState";
        public const string ExternalDownloadClientId = "externalDownloadClientId";
        public const string ExternalIndexerId = "externalIndexerId";
    }
}
