using Serilog;

namespace Reaparr.Data.Contracts;

public static partial class DbContextExtensions
{
    private static readonly ILogger _log = LogFactory.Create(typeof(DbContextExtensions));

    public static async Task<Result<PlexServerConnection>> ChoosePlexServerConnection(
        this IReaparrDbContext dbContext,
        int plexServerId,
        CancellationToken cancellationToken = default
    )
    {
        if (plexServerId <= 0)
            return ResultExtensions.IsInvalidId(nameof(PlexServer), plexServerId);

        var plexServer = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .AsNoTracking()
            .Include(x => x.PlexServerConnections)
                .ThenInclude(x => x.LatestConnectionStatus)
            .FirstOrDefaultAsync(x => x.Id == plexServerId, cancellationToken);

        if (plexServer is null)
            return ResultExtensions.EntityNotFound(nameof(PlexServer), plexServerId);

        return plexServer.ChoosePlexServerConnection();
    }

    public static Result<PlexServerConnection> ChoosePlexServerConnection(this PlexServer plexServer)
    {
        if (!plexServer.IsEnabled)
        {
            return _log.Here()
                .ErrorResult(
                    "Cannot choose PlexServer connection, server {ServerName} with id {Id} is disabled",
                    plexServer.Name,
                    plexServer.Id
                );
        }

        var plexServerConnections = plexServer.PlexServerConnections;
        if (!plexServerConnections.Any())
        {
            return _log.Here()
                .ErrorResult(
                    "PlexServer with id {PlexServerId} and name {PlexServerName} has no connections available!",
                    plexServer.Id,
                    plexServer.Name
                );
        }

        if (plexServerConnections.All(x => !x.IsOnline))
        {
            return _log.Here()
                .WarningResult(
                    "PlexServer with id {PlexServerId} and name {PlexServerName} has no online connections available!",
                    plexServer.Id,
                    plexServer.Name
                )
                .Add504GatewayTimeoutError();
        }

        return ChooseOnlineConnection(plexServer, plexServerConnections.Where(x => x.IsOnline).ToList());
    }

    private static Result<PlexServerConnection> ChooseOnlineConnection(
        PlexServer plexServer,
        List<PlexServerConnection> successPlexServerConnections
    )
    {

        // Find based on preference
        if (plexServer.PreferredConnectionId > 0)
        {
            var connection = successPlexServerConnections.Find(x => x.Id == plexServer.PreferredConnectionId);
            if (connection is not null)
                return Result.Ok(connection);

            _log.Here()
                .Verbose(
                    "Could not find preferred connection with id {PlexServerConnectionId} for server {PlexServerName}",
                    plexServer.PreferredConnectionId,
                    plexServer.Name
                );
        }

        // Find HTTPS connections first
        _log.Here()
            .Verbose(
                "Attempting to find an HTTPS PlexServerConnection that is online for server {PlexServerName}",
                plexServer.Name
            );

        var httpsConnection = successPlexServerConnections.FirstOrDefault(x => x.IsHttps);
        if (httpsConnection is not null)
            return Result.Ok(httpsConnection);

        // Find based on online local address, because that is the fastest
        _log.Here()
            .Verbose(
                "Attempting to find a local PlexServerConnection that is online for server {PlexServerName}",
                plexServer.Name
            );

        var localConnection = successPlexServerConnections.FirstOrDefault(x => x.Local);
        if (localConnection is not null)
            return Result.Ok(localConnection);

        // Find based on public address
        _log.Here()
            .Verbose(
                "Attempting to find PlexServerConnection that matches the PlexServer public address: {PublicAddress}",
                plexServer.PublicAddress
            );
        var publicConnection = successPlexServerConnections.FirstOrDefault(x => x.Address == plexServer.PublicAddress);
        if (publicConnection is not null)
            return Result.Ok(publicConnection);

        // Give preference to non-PlexTv connections
        var directConnections = successPlexServerConnections.Where(x => !x.IsPlexTvConnection).ToList();
        if (directConnections.Any())
        {
            _log.Here()
                .Verbose(
                    "Found a direct connection that was successful. We're gonna use that: {DirectConnection}",
                    directConnections.First()
                );
            return Result.Ok(directConnections.First());
        }

        return Result.Ok(successPlexServerConnections.First());
    }

    /// <summary>
    /// Returns an authentication token needed to authenticate communication with the <see cref="PlexServer" />.
    /// Note: An PlexAccountId of 0 can be passed to automatically retrieve first a non-main account token, and if not found a main account server token.
    /// </summary>
    /// <param name="dbContext"> The <see cref="IReaparrDbContext" /> to retrieve the token from.</param>
    /// <param name="plexServerId">The id of the <see cref="PlexServer" /> to retrieve a token for.</param>
    /// <param name="cancellationToken"> The cancellation token to cancel operation.</param>
    public static Task<Result<string>> GetPlexServerTokenAsync(
        this IReaparrDbContext dbContext,
        int plexServerId,
        CancellationToken cancellationToken = default
    ) => dbContext.GetPlexServerTokenAsync(plexServerId, 0, cancellationToken);

    /// <summary>
    ///  Returns the authentication token needed to authenticate communication with the <see cref="PlexServer" />.
    /// Note: An PlexAccountId of 0 can be passed to automatically retrieve first a non-main account token, and if not found a main account server token.
    /// </summary>
    /// <param name="dbContext">The <see cref="IReaparrDbContext" /> to retrieve the token from.</param>
    /// <param name="plexServerId">The id of the <see cref="PlexServer" /> to retrieve a token for.</param>
    /// <param name="plexAccountId"> An PlexAccountId of 0 can be passed to automatically retrieve first a non-main account token, and if not found a main account server token.</param>
    /// <param name="cancellationToken"> The cancellation token to cancel operation.</param>
    public static async Task<Result<string>> GetPlexServerTokenAsync(
        this IReaparrDbContext dbContext,
        int plexServerId,
        int plexAccountId = 0,
        CancellationToken cancellationToken = default
    )
    {
        // Attempt to find a non-main account token first
        if (plexAccountId == 0)
        {
            var nonMainServerToken = await dbContext
                .PlexAccountServers.Include(x => x.PlexAccount)
                .FirstOrDefaultAsync(
                    x =>
                        x.PlexServerId == plexServerId
                        && !x.PlexAccount!.IsMain
                        && x.PlexAccount.IsValidated
                        && x.PlexAccount.IsEnabled,
                    cancellationToken
                );

            // Check if we have access with a validated non-main account
            if (nonMainServerToken != null)
                return Result.Ok(nonMainServerToken.AuthToken);

            // Fallback to a validated main-account access
            var mainServerToken = await dbContext
                .PlexAccountServers.Include(x => x.PlexAccount)
                .FirstOrDefaultAsync(
                    x => x.PlexServerId == plexServerId && x.PlexAccount!.IsValidated && x.PlexAccount.IsEnabled,
                    cancellationToken
                );

            if (mainServerToken != null)
                return Result.Ok(mainServerToken.AuthToken);

            return Result
                .Fail("Could not find any authenticationToken for PlexServer with id: {PlexServerId}", plexServerId)
                .AddPlex401UnauthorizedError()
                .LogError();
        }

        var isAccountValidated = await dbContext.PlexAccounts.AnyAsync(
            x => x.Id == plexAccountId && x.IsValidated,
            cancellationToken
        );

        if (!isAccountValidated)
            return Result.Fail("PlexAccount with id: {PlexAccountId} is not validated", plexAccountId).LogError();

        var authToken = await dbContext.PlexAccountServers.FirstOrDefaultAsync(
            x => x.PlexAccountId == plexAccountId && x.PlexServerId == plexServerId,
            cancellationToken
        );

        if (authToken != null)
            return Result.Ok(authToken.AuthToken);

        return Result
            .Fail(
                new Error(
                    $"Could not find an authenticationToken for PlexAccount with id: {plexAccountId} and PlexServer with id: {plexServerId}"
                )
            )
            .LogError();
    }
}
