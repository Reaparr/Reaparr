namespace Reaparr.Data.Contracts;

public sealed record PlexLibraryOwnership(int Id, PlexMediaType Type, bool IsOwned);

public static class DbSetExtensionsPlexLibrary
{
    public static IQueryable<PlexLibrary> IgnoreIsEnabledFilter(this IQueryable<PlexLibrary> query) =>
        query.IgnoreQueryFilters(["IsEnabled"]);

    /// <summary>
    /// Projects <see cref="PlexLibrary"/> rows with the shared ownership decision.
    /// </summary>
    /// <remarks>
    /// A library is owned when:
    /// <list type="number">
    ///   <item><c>PlexServer.OwnedOverride == true</c>, or</item>
    ///   <item><c>PlexServer.OwnedOverride</c> is null AND
    ///     (<c>PlexAccountLibraries.Any(y => y.IsLibraryOwned)</c>
    ///     OR <c>PlexServer.PlexAccountServers.Any(y => y.IsServerOwned)</c>)</item>
    /// </list>
    /// </remarks>
    public static IQueryable<PlexLibraryOwnership> SelectOwnership(this IQueryable<PlexLibrary> query) =>
        query.Select(library => new PlexLibraryOwnership(
            library.Id,
            library.Type,
            library.PlexServer!.OwnedOverride
                ?? (
                    library.PlexAccountLibraries.Any(link => link.IsLibraryOwned)
                    || library.PlexServer.PlexAccountServers.Any(link => link.IsServerOwned)
                )
        ));

    public static IQueryable<PlexLibrary> WhereIsOwned(this IQueryable<PlexLibrary> query) =>
        query.Where(library =>
            library.PlexServer!.OwnedOverride
            ?? (
                library.PlexAccountLibraries.Any(link => link.IsLibraryOwned)
                || library.PlexServer.PlexAccountServers.Any(link => link.IsServerOwned)
            )
        );

    /// <summary>
    /// Filters <see cref="PlexLibrary"/> to only those NOT owned by any linked Plex account.
    /// </summary>
    /// <remarks>
    /// A library is not owned when:
    /// <list type="number">
    ///   <item><c>PlexServer.OwnedOverride == false</c>, or</item>
    ///   <item><c>PlexServer.OwnedOverride</c> is null AND
    ///     (no <c>PlexAccountLibrary.IsLibraryOwned</c>
    ///     AND no <c>PlexAccountServer.IsServerOwned</c>)</item>
    /// </list>
    /// </remarks>
    public static IQueryable<PlexLibrary> WhereIsNotOwned(this IQueryable<PlexLibrary> query) =>
        query.Where(library =>
            !(
                library.PlexServer!.OwnedOverride
                ?? (
                    library.PlexAccountLibraries.Any(link => link.IsLibraryOwned)
                    || library.PlexServer.PlexAccountServers.Any(link => link.IsServerOwned)
                )
            )
        );
}
