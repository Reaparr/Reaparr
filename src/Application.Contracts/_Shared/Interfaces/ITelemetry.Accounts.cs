namespace Reaparr.Application.Contracts;

public partial interface ITelemetry
{
    /// <summary>
    /// Records a Plex account change.
    /// </summary>
    void PlexAccountChanged(TelemetryAction action, int accountId, PlexAccount? account);

    /// <summary>
    /// Records the result of Plex token authentication.
    /// </summary>
    void PlexAuthenticationProcessed(
        int? accountId,
        Result<ValidatePlexTokenCommandResult> result
    );

    /// <summary>
    /// Records the result of Plex authentication.
    /// </summary>
    void PlexAuthenticationProcessed(
        TelemetryAuthenticationMethod method,
        int? accountId,
        ResultBase result
    );

    /// <summary>
    /// Records a requested Plex access refresh.
    /// </summary>
    void PlexAccessRefreshRequested(
        TelemetryScope scope,
        Result<List<RefreshPlexAccountAccessRapportDTO>> result,
        int? accountId = null,
        int? serverId = null,
        int accountCount = 0
    );

    /// <summary>
    /// Records a requested Plex access refresh.
    /// </summary>
    void PlexAccessRefreshRequested(
        TelemetryScope scope,
        ResultBase result,
        int? accountId = null,
        int? serverId = null,
        int accountCount = 0
    );

    /// <summary>
    /// Records completion of a Plex access refresh.
    /// </summary>
    void PlexAccessRefreshFinished(
        int accountId,
        TelemetryTrigger trigger,
        Result<ValidatePlexTokenCommandResult> result,
        RefreshPlexAccountAccessRapportDTO? rapport = null
    );

    /// <summary>
    /// Records completion of a Plex access refresh.
    /// </summary>
    void PlexAccessRefreshFinished(
        int accountId,
        TelemetryTrigger trigger,
        Result<PlexLibraryAccessRefreshResponse> result,
        RefreshPlexAccountAccessRapportDTO? rapport = null
    );

    /// <summary>
    /// Records completion of a Plex access refresh.
    /// </summary>
    void PlexAccessRefreshFinished(
        int accountId,
        TelemetryTrigger trigger,
        ResultBase result,
        RefreshPlexAccountAccessRapportDTO? rapport = null
    );

    /// <summary>
    /// Records a Plex server change.
    /// </summary>
    void PlexServerChanged(TelemetryAction action, int serverId, PlexServer? server);

    /// <summary>
    /// Records a Plex server alias change.
    /// </summary>
    void PlexServerAliasChanged(int serverId, string machineIdentifier, string alias);

    /// <summary>
    /// Records a server inspection request.
    /// </summary>
    void ServerInspectionRequested(int serverId, ResultBase result);

    /// <summary>
    /// Records the start of server inspection.
    /// </summary>
    void ServerInspectionStarted(int serverId);

    /// <summary>
    /// Records completion of server inspection.
    /// </summary>
    void ServerInspectionFinished(int serverId, ResultBase result);

    /// <summary>
    /// Records a server download control action.
    /// </summary>
    void ServerDownloadControlProcessed(int serverId, TelemetryAction action, bool isPaused);

    /// <summary>
    /// Records a Plex server connection change.
    /// </summary>
    void PlexConnectionChanged(
        TelemetryAction action,
        PlexServerConnection connection,
        int? preferredConnectionId
    );

    /// <summary>
    /// Records completion of a single connection check.
    /// </summary>
    void ConnectionCheckFinished(int connectionId, Result<PlexServerStatus> result);

    /// <summary>
    /// Records completion of an aggregate connection check.
    /// </summary>
    void ConnectionCheckFinished(int serverId, Result<List<PlexServerStatus>> result);

    /// <summary>
    /// Records completion of an explicit connection URL check.
    /// </summary>
    void ConnectionCheckFinished(ResultBase result);
}
