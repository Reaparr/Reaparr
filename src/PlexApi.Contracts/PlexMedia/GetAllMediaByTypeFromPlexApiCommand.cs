namespace Reaparr.PlexApi.Contracts;

public record GetAllMediaByTypeFromPlexApiCommand(
    PlexLibrary PlexLibrary,
    PlexMediaType MediaType,
    int BatchSize = 1000
) : ICommand<Result<List<LibraryMediaItemDTO>>>;
