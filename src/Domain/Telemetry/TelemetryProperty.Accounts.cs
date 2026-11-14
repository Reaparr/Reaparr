namespace Reaparr.Domain;

public static partial class TelemetryProperty
{
    /// <summary>
    /// Groups keys for Plex account, server, and connection data. Used by the telemetry facade
    /// to describe configuration changes, access refreshes, and connection checks.
    /// </summary>
    public static class Accounts
    {
        public const string Account = "account";
        public const string Server = "server";
        public const string Connection = "connection";
        public const string PlexServerConnectionId = "plex_server_connection_id";
        public const string AccountCount = "account_count";
        public const string GrantedCount = "granted_count";
        public const string UpdatedCount = "updated_count";
        public const string RevokedCount = "revoked_count";
        public const string AccessCount = "access_count";
        public const string MachineIdentifier = "machine_identifier";
        public const string IsDownloadsPausedByUser = "is_downloads_paused_by_user";
        public const string IsEnabled = "is_enabled";
        public const string OwnedOverride = "owned_override";
        public const string Owned = "owned";
        public const string Alias = "alias";
        public const string IsPreferred = "is_preferred";
        public const string Id = "id";
        public const string DisplayName = "displayName";
        public const string Username = "username";
        public const string PlexId = "plexId";
        public const string Uuid = "uuid";
        public const string Name = "name";
        public const string PreferredConnectionId = "preferredConnectionId";
        public const string PlexServerId = "plexServerId";
        public const string Protocol = "protocol";
        public const string Address = "address";
        public const string Port = "port";
        public const string Url = "url";
        public const string IsCustom = "isCustom";
        public const string Local = "local";
        public const string Relay = "relay";
        public const string Ipv4 = "ipv4";
        public const string Ipv6 = "ipv6";
        public const string IsPlexTvConnection = "isPlexTvConnection";
        public const string Type = "type";
        public const string IsMain = "isMain";
        public const string IsValidated = "isValidated";
    }
}
