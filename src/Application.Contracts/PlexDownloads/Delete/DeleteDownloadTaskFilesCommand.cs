namespace Reaparr.Application.Contracts;

/// <summary>
/// Deletes the source files from the download directory for the given file-task keys.
/// This is needed when an external tool (e.g. Sonarr/Radarr) has already imported the files
/// and Reaparr receives a delete-torrent request with <c>deleteFiles=true</c> for tasks that are
/// not actively downloading/moving. Those non-active tasks are deleted without stop semantics,
/// so this command explicitly removes files that may still be present in the download directory.
/// Only file-type keys (<see cref="DownloadTaskType.MovieData"/>, <see cref="DownloadTaskType.MoviePart"/>,
/// </summary>
public record DeleteDownloadTaskFilesCommand(List<DownloadTaskKey> Keys) : ICommand<Result>;
