namespace Reaparr.PlexApi.Contracts;

/// <summary>
/// Retrieves and maps library roots, or one explicitly requested descendant type.
/// Descendant retrieval uses the supplied library's parents without refreshing sections or restarting progress.
/// </summary>
public record GetLibraryMediaFromPlexApiCommand(PlexLibrary PlexLibrary, PlexMediaType? MediaType = null)
    : ICommand<Result<LibraryMetadata>>;
