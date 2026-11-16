namespace Reaparr.PlexApi.Contracts;

public record GetLibraryMediaCommand(PlexLibrary PlexLibrary, Func<MediaSyncProgress, Task>? Action = null)
    : ICommand<Result<LibraryMetadata>>;
