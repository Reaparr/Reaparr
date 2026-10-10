namespace Reaparr.Application.Contracts;

/// <summary>
/// Deletes download task rows from the database for all given keys, regardless of status,
/// and removes any orphaned parent tasks that no longer have children.
/// </summary>
/// <remarks>
/// This is the single central point for all unconditional download-task DB deletion.
/// Each key carries its <see cref="DownloadTaskType"/> so the handler can route deletions
/// to the correct table without scattering the same ID list across unrelated tables.
/// Callers are responsible for stopping active downloads and cleaning up files on disk
/// <em>before</em> dispatching this command.
/// </remarks>
public record DeleteDownloadTasksByKeyCommand(List<DownloadTaskKey> Keys) : ICommand<Result>;
