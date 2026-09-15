namespace Reaparr.Data.Contracts;

public static partial class DbContextExtensions
{
    public static async Task<string> GetPlexServerNameById(this IReaparrDbContext dbContext, int plexServerId)
    {
        var plexServerName = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == plexServerId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(CancellationToken.None);
        return plexServerName ?? "Server Name Not Found";
    }

    public static async Task<string> GetPlexServerMachineIdentifierById(
        this IReaparrDbContext dbContext,
        int plexServerId
    )
    {
        var plexServer = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .GetAsync(plexServerId, cancellationToken: CancellationToken.None);
        return plexServer?.MachineIdentifier ?? string.Empty;
    }

    public static async Task<bool> IsServerOnline(this IReaparrDbContext dbContext, int plexServerId)
    {
        return await dbContext
            .PlexServerStatuses.Where(x => x.PlexServerId == plexServerId && x.IsSuccessful)
            .AnyAsync(CancellationToken.None);
    }

    /// <summary>
    /// Returns whether a <see cref="PlexServer"/> exists and is disabled.
    /// </summary>
    public static async Task<bool> IsServerDisabled(this IReaparrDbContext dbContext, int plexServerId)
    {
        var isEnabled = await dbContext
            .PlexServers.IgnoreIsEnabledFilter() // Include disabled rows so we can distinguish disabled from non-existent servers.
            .Where(x => x.Id == plexServerId)
            .Select(x => (bool?)x.IsEnabled)
            .FirstOrDefaultAsync(CancellationToken.None);

        return isEnabled.HasValue && !isEnabled.Value;
    }

    /// <summary>
    /// Check if the <see cref="PlexServer"/> has globally paused all downloads by the user
    /// </summary>
    public static async Task<bool> IsDownloadsPausedByUser(this IReaparrDbContext dbContext, int plexServerId)
    {
        return await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .AsNoTracking()
            .Where(x => x.Id == plexServerId)
            .Select(x => x.IsDownloadsPausedByUser)
            .FirstOrDefaultAsync(CancellationToken.None);
    }

    public static async Task<List<int>> GetOnlineServerIds(this IReaparrDbContext dbContext)
    {
        return await dbContext
            .PlexServerStatuses.AsNoTracking()
            .Where(x => x.IsSuccessful)
            .Select(x => x.PlexServerId)
            .Distinct()
            .ToListAsync(CancellationToken.None);
    }

    /// <summary>
    /// Servers that are online AND whose downloads are not paused by the user.
    ///
    /// The Torznab indexer should only advertise media it can actually deliver. Media from a
    /// paused server yields releases that Sonarr/Radarr happily grab and then wait on
    /// forever, because the download task is created and never picked up.
    /// </summary>
    public static async Task<List<int>> GetDownloadableServerIds(this IReaparrDbContext dbContext)
    {
        return await dbContext
            .PlexServerStatuses.AsNoTracking()
            .Where(x =>
                x.IsSuccessful
                && x.PlexServer!.IsEnabled
                && !x.PlexServer.IsDownloadsPausedByUser
            )
            .Select(x => x.PlexServerId)
            .Distinct()
            .ToListAsync(CancellationToken.None);
    }

    public static Task<List<int>> GetAccessibleLibraryIds(
        this IReaparrDbContext dbContext,
        IReadOnlyCollection<int> allowedServerIds,
        CancellationToken cancellationToken
    ) =>
        dbContext
            .PlexLibraries.Where(x =>
                allowedServerIds.Contains(x.PlexServerId)
                && x.PlexAccountLibraries.Any(libraryAccess =>
                    x.PlexServer!.PlexAccountServers.Any(serverAccess =>
                        serverAccess.PlexAccountId == libraryAccess.PlexAccountId
                    )
                )
            )
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
}
