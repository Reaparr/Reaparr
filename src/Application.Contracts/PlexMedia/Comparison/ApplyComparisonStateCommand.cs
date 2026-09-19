namespace Reaparr.Application.Contracts;

/// <summary>
/// Projects stored comparison scopes and hit rows onto <see cref="PlexMediaSlimDTO"/> items
/// for one explicit library or all libraries represented by the items, setting
/// <see cref="PlexMediaSlimDTO.ComparisonId"/> per item in-place.
/// Annotates each item with comparison state derived from per-owned-target hit coverage.
/// </summary>
/// <param name="Items">The overview page items. Modified in-place.</param>
/// <param name="MediaType">Movie or TvShow.</param>
/// <param name="PlexLibraryId">The explicit Plex library being browsed. Null derives library scopes from <paramref name="Items"/>.</param>
public record ApplyComparisonStateCommand(
    List<PlexMediaSlimDTO> Items,
    PlexMediaType MediaType,
    int? PlexLibraryId
) : ICommand<Result>;
