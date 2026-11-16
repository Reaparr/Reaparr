namespace Reaparr.Domain;

/// <summary>
/// Defines shared business-event property names and trace tags. The telemetry facade uses these keys,
/// including the nested feature groups, when constructing telemetry payloads.
/// </summary>
public static partial class TelemetryProperty
{
    public const string Action = "action";
    public const string Outcome = "outcome";
    public const string Stage = "stage";
    public const string Trigger = "trigger";
    public const string Scope = "scope";
    public const string Mode = "mode";
    public const string Method = "method";
    public const string Health = "health";
    public const string Status = "status";
    public const string Origin = "origin";
    public const string Environment = "environment";
    public const string Release = "release";
    public const string RuntimeMode = "runtime_mode";
    public const string Platform = "platform";
    public const string RuntimeIdentifier = "runtime_identifier";
    public const string Provider = "provider";
    public const string AccountId = "account_id";
    public const string ServerId = "server_id";
    public const string LibraryId = "library_id";
    public const string FolderId = "folder_id";
    public const string IntegrationId = "integration_id";
    public const string MediaId = "media_id";
    public const string MediaType = "media_type";
    public const string DataId = "data_id";
    public const string PartId = "part_id";
    public const string PlexApiPartId = "plex_api_part_id";
    public const string TaskId = "task_id";
    public const string TaskIds = "task_ids";
    public const string TaskKeys = "task_keys";
    public const string HashId = "hash_id";
    public const string RefreshRunId = "refresh_run_id";
    public const string CreatedAt = "created_at";
    public const string JobName = "job_name";
    public const string FireInstanceId = "fire_instance_id";
    public const string DisplayName = "display_name";
    public const string Path = "path";
    public const string DirectoryPath = "directory_path";
    public const string FileName = "file_name";
    public const string FileSize = "file_size";
    public const string Query = "query";
    public const string Page = "page";
    public const string PageSize = "page_size";
    public const string ReturnedItemCount = "returned_item_count";
    public const string TotalCount = "total_count";
    public const string TotalBytes = "total_bytes";
    public const string MatchedCount = "matched_count";
    public const string DeletedCount = "deleted_count";
    public const string DeleteFiles = "delete_files";
    public const string Version = "version";
    public const string HttpMethod = "http_method";
    public const string HttpRoute = "http_route";
    public const string HttpStatusCode = "http_status_code";
}
