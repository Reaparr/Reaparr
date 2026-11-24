namespace Reaparr.Application;

public sealed partial class Telemetry
{
    /// <inheritdoc/>
    public void PlexAccountChanged(TelemetryAction action, int accountId, PlexAccount? account)
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Action] = action.ToString(),
            [TelemetryProperty.AccountId] = accountId,
        };

        if (account is not null)
            properties[TelemetryProperty.Accounts.Account] = AccountProperties(account);

        Capture(TelemetryEvent.PlexAccountChanged, properties);
    }

    /// <inheritdoc/>
    public void PlexAuthenticationProcessed(
        int? accountId,
        ResultBase result,
        PlexAuthenticationTelemetryDTO? authentication = null
    )
    {
        var outcome = AuthenticationOutcome(TelemetryAuthenticationMethod.Token, authentication, result);
        CapturePlexAuthenticationProcessed(TelemetryAuthenticationMethod.Token, accountId, outcome);
    }

    /// <inheritdoc/>
    public void PlexAuthenticationProcessed(
        TelemetryAuthenticationMethod method,
        int? accountId,
        ResultBase result,
        PlexAuthenticationTelemetryDTO? authentication = null
    )
    {
        var outcome = AuthenticationOutcome(method, authentication, result);
        CapturePlexAuthenticationProcessed(method, accountId, outcome);
    }

    private static TelemetryOutcome AuthenticationOutcome(
        TelemetryAuthenticationMethod method,
        PlexAuthenticationTelemetryDTO? authentication,
        ResultBase result
    )
    {
        if (result.IsCancelled)
            return TelemetryOutcome.Cancelled;

        if (result.Errors.Any(error => error is PlexError { Code: PlexErrorCodes.EnterVerificationCode }))
            return TelemetryOutcome.NotRequired;

        if (
            result.HasStatusCode(
                method == TelemetryAuthenticationMethod.GenerateToken
                    ? StatusCodes.Status401Unauthorized
                    : HttpCodes.Status601PlexAuthenticationFailed
            )
        )
            return TelemetryOutcome.Rejected;

        return result.IsSuccess
            ? authentication is { IsValidated: false }
                ? TelemetryOutcome.Rejected
                : TelemetryOutcome.Succeeded
            : TelemetryOutcome.Failed;
    }

    private void CapturePlexAuthenticationProcessed(
        TelemetryAuthenticationMethod method,
        int? accountId,
        TelemetryOutcome outcome
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Method] = method.ToString(),
            [TelemetryProperty.Outcome] = outcome.ToString(),
            [TelemetryProperty.AccountId] = accountId!,
        };

        Capture(TelemetryEvent.PlexAuthenticationProcessed, properties);
    }

    /// <inheritdoc/>
    public void PlexAccessRefreshRequested(
        TelemetryScope scope,
        Result<List<RefreshPlexAccountAccessRapportDTO>> result,
        int? accountId = null,
        int? serverId = null,
        int accountCount = 0
    )
    {
        var outcome = AccessRefreshRequestedOutcome(scope, result, serverId, accountCount, result.ValueOrDefault);
        CapturePlexAccessRefreshRequested(scope, accountId, serverId, accountCount, outcome);
    }

    /// <inheritdoc/>
    public void PlexAccessRefreshRequested(
        TelemetryScope scope,
        ResultBase result,
        int? accountId = null,
        int? serverId = null,
        int accountCount = 0
    )
    {
        var outcome = AccessRefreshRequestedOutcome(scope, result, serverId, accountCount, null);
        CapturePlexAccessRefreshRequested(scope, accountId, serverId, accountCount, outcome);
    }

    private static TelemetryOutcome AccessRefreshRequestedOutcome(
        TelemetryScope scope,
        ResultBase result,
        int? serverId,
        int accountCount,
        IReadOnlyCollection<RefreshPlexAccountAccessRapportDTO>? reports
    )
    {
        if (result.IsCancelled)
            return TelemetryOutcome.Cancelled;

        if (result.IsFailed)
        {
            if (
                scope == TelemetryScope.Server
                && accountCount == 0
                && result.HasStatusCode(StatusCodes.Status409Conflict)
            )
                return TelemetryOutcome.Rejected;

            return scope == TelemetryScope.Server && reports is { Count: > 0 }
                ? TelemetryOutcome.Partial
                : TelemetryOutcome.Failed;
        }

        return
            scope == TelemetryScope.Server && accountCount == 0
            || scope == TelemetryScope.Account && serverId is null && reports is { Count: 0 }
            ? TelemetryOutcome.NoOp
            : TelemetryOutcome.Accepted;
    }

    private void CapturePlexAccessRefreshRequested(
        TelemetryScope scope,
        int? accountId,
        int? serverId,
        int accountCount,
        TelemetryOutcome outcome
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Scope] = scope.ToString(),
            [TelemetryProperty.Trigger] = nameof(TelemetryTrigger.Manual),
            [TelemetryProperty.Outcome] = outcome.ToString(),
            [TelemetryProperty.AccountId] = accountId!,
            [TelemetryProperty.ServerId] = serverId!,
        };

        if (accountCount > 0)
            properties[TelemetryProperty.Accounts.AccountCount] = accountCount;

        Capture(TelemetryEvent.PlexAccessRefreshRequested, properties);
    }

    /// <inheritdoc/>
    public void PlexAccessRefreshValidationFinished(int accountId, TelemetryTrigger trigger, ResultBase result)
    {
        var outcome =
            result.IsCancelled ? TelemetryOutcome.Cancelled
            : result.IsFailed ? TelemetryOutcome.Failed
            : TelemetryOutcome.Succeeded;
        CapturePlexAccessRefreshFinished(accountId, trigger, outcome, null);
    }

    /// <inheritdoc/>
    public void PlexAccessRefreshFinished(
        int accountId,
        TelemetryTrigger trigger,
        Result<PlexLibraryAccessRefreshResponse> result,
        RefreshPlexAccountAccessRapportDTO? rapport = null
    )
    {
        var outcome =
            result.IsCancelled ? TelemetryOutcome.Cancelled
            : result.IsFailed ? TelemetryOutcome.Partial
            : result.Value.OfflineServers.Count > 0 ? TelemetryOutcome.Partial
            : TelemetryOutcome.Succeeded;
        CapturePlexAccessRefreshFinished(accountId, trigger, outcome, rapport);
    }

    /// <inheritdoc/>
    public void PlexAccessRefreshFinished(
        int accountId,
        TelemetryTrigger trigger,
        ResultBase result,
        RefreshPlexAccountAccessRapportDTO? rapport = null
    )
    {
        var outcome =
            result.IsCancelled ? TelemetryOutcome.Cancelled
            : result.IsFailed ? TelemetryOutcome.Partial
            : TelemetryOutcome.Succeeded;
        CapturePlexAccessRefreshFinished(accountId, trigger, outcome, rapport);
    }

    private void CapturePlexAccessRefreshFinished(
        int accountId,
        TelemetryTrigger trigger,
        TelemetryOutcome outcome,
        RefreshPlexAccountAccessRapportDTO? rapport
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.AccountId] = accountId,
            [TelemetryProperty.Trigger] = trigger.ToString(),
            [TelemetryProperty.Outcome] = outcome.ToString(),
        };

        if (rapport is not null)
        {
            properties[TelemetryProperty.Accounts.GrantedCount] = rapport.Access.Count(x =>
                x.State == PlexAccessState.Granted
            );
            properties[TelemetryProperty.Accounts.UpdatedCount] = rapport.Access.Count(x =>
                x.State == PlexAccessState.Updated
            );
            properties[TelemetryProperty.Accounts.RevokedCount] = rapport.Access.Count(x =>
                x.State == PlexAccessState.Revoked
            );
            properties[TelemetryProperty.Accounts.AccessCount] = rapport.Access.Count;
        }

        Capture(TelemetryEvent.PlexAccessRefreshFinished, properties);
    }

    /// <inheritdoc/>
    public void PlexServerChanged(TelemetryAction action, int serverId, PlexServer? server)
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Action] = action.ToString(),
            [TelemetryProperty.ServerId] = serverId,
        };

        if (server is not null)
            properties[TelemetryProperty.Accounts.Server] = ServerProperties(server);

        Capture(TelemetryEvent.PlexServerChanged, properties);
    }

    /// <inheritdoc/>
    public void PlexServerAliasChanged(int serverId, string machineIdentifier, string alias)
    {
        Capture(
            TelemetryEvent.PlexServerChanged,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Action] = nameof(TelemetryAction.Alias),
                [TelemetryProperty.ServerId] = serverId,
                [TelemetryProperty.Accounts.MachineIdentifier] = machineIdentifier,
                [TelemetryProperty.Accounts.Alias] = alias,
            }
        );
    }

    /// <inheritdoc/>
    public void ServerInspectionRequested(int serverId, ResultBase result) =>
        Capture(
            TelemetryEvent.ServerInspectionRequested,
            new Dictionary<string, object>
            {
                [TelemetryProperty.ServerId] = serverId,
                [TelemetryProperty.Outcome] = (
                    result.IsCancelled ? TelemetryOutcome.Cancelled
                    : result.IsFailed ? TelemetryOutcome.Rejected
                    : TelemetryOutcome.Accepted
                ).ToString(),
            }
        );

    /// <inheritdoc/>
    public void ServerInspectionStarted(int serverId) =>
        Capture(
            TelemetryEvent.ServerInspectionStarted,
            new Dictionary<string, object>
            {
                [TelemetryProperty.ServerId] = serverId,
                [TelemetryProperty.Outcome] = nameof(TelemetryOutcome.Succeeded),
            }
        );

    /// <inheritdoc/>
    public void ServerInspectionFinished(int serverId, ResultBase result) =>
        Capture(
            TelemetryEvent.ServerInspectionFinished,
            new Dictionary<string, object>
            {
                [TelemetryProperty.ServerId] = serverId,
                [TelemetryProperty.Outcome] = (
                    result.IsCancelled ? TelemetryOutcome.Cancelled
                    : result.IsFailed ? TelemetryOutcome.Failed
                    : TelemetryOutcome.Succeeded
                ).ToString(),
            }
        );

    /// <inheritdoc/>
    public void ServerDownloadControlProcessed(int serverId, TelemetryAction action, bool isPaused) =>
        Capture(
            TelemetryEvent.ServerDownloadControlProcessed,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Action] = action.ToString(),
                [TelemetryProperty.ServerId] = serverId,
                [TelemetryProperty.Accounts.IsDownloadsPausedByUser] = isPaused,
                [TelemetryProperty.Outcome] = nameof(TelemetryOutcome.Succeeded),
            }
        );

    /// <inheritdoc/>
    public void PlexConnectionChanged(
        TelemetryAction action,
        PlexServerConnection connection,
        int? preferredConnectionId
    )
    {
        Capture(
            TelemetryEvent.PlexConnectionChanged,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Action] = action.ToString(),
                [TelemetryProperty.ServerId] = connection.PlexServerId,
                [TelemetryProperty.Accounts.Connection] = ConnectionProperties(connection, preferredConnectionId),
                [TelemetryProperty.Accounts.PlexServerConnectionId] = connection.Id,
            }
        );
    }

    /// <inheritdoc/>
    public void ConnectionCheckFinished(int connectionId, Result<PlexServerStatus> result)
    {
        var status = result.ValueOrDefault;
        var outcome =
            result.IsCancelled ? TelemetryOutcome.Cancelled
            : result.IsFailed ? TelemetryOutcome.Failed
            : TelemetryOutcome.Succeeded;
        var health = result.IsSuccess
            ? result.Value.IsSuccessful
                ? TelemetryHealth.Healthy
                : TelemetryHealth.Unhealthy
            : TelemetryHealth.Unknown;

        Capture(
            TelemetryEvent.PlexConnectionCheckFinished,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Accounts.PlexServerConnectionId] = connectionId,
                [TelemetryProperty.ServerId] = status?.PlexServerId!,
                [TelemetryProperty.Outcome] = outcome.ToString(),
                [TelemetryProperty.Health] = health.ToString(),
            }
        );
    }

    /// <inheritdoc/>
    public void ConnectionCheckFinished(int serverId, Result<List<PlexServerStatus>> result)
    {
        var statuses = result.ValueOrDefault;
        var hasStatuses = statuses is { Count: > 0 };
        var allUnhealthy = hasStatuses && statuses!.All(status => !status.IsSuccessful);
        var outcome =
            result.IsCancelled ? TelemetryOutcome.Cancelled
            : allUnhealthy ? TelemetryOutcome.Succeeded
            : result.IsFailed
                ? result.HasError<ExceptionalError>() ? TelemetryOutcome.Failed
                    : TelemetryOutcome.Rejected
            : TelemetryOutcome.Succeeded;
        var health =
            !hasStatuses ? TelemetryHealth.Unknown
            : statuses!.Any(status => status.IsSuccessful) ? TelemetryHealth.Healthy
            : TelemetryHealth.Unhealthy;

        Capture(
            TelemetryEvent.PlexConnectionCheckFinished,
            new Dictionary<string, object>
            {
                [TelemetryProperty.ServerId] = serverId,
                [TelemetryProperty.Outcome] = outcome.ToString(),
                [TelemetryProperty.Health] = health.ToString(),
            }
        );
    }

    /// <inheritdoc/>
    public void ConnectionCheckFinished(ResultBase result) =>
        Capture(
            TelemetryEvent.PlexConnectionCheckFinished,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Outcome] = Outcome(result),
                [TelemetryProperty.Health] = (
                    result.IsSuccess ? TelemetryHealth.Healthy : TelemetryHealth.Unknown
                ).ToString(),
            }
        );

    private static Dictionary<string, object> AccountProperties(PlexAccount account) =>
        new()
        {
            [TelemetryProperty.Accounts.Id] = account.Id,
            [TelemetryProperty.Accounts.AccountDisplayName] = account.DisplayName,
            [TelemetryProperty.Accounts.Username] = account.Username,
            [TelemetryProperty.Accounts.PlexId] = account.PlexId,
            [TelemetryProperty.Accounts.Uuid] = account.Uuid,
            [TelemetryProperty.Accounts.IsEnabled] = account.IsEnabled,
            [TelemetryProperty.Accounts.IsMain] = account.IsMain,
            [TelemetryProperty.Accounts.IsValidated] = account.IsValidated,
        };

    private static Dictionary<string, object> ServerProperties(PlexServer server) =>
        new()
        {
            [TelemetryProperty.Accounts.Id] = server.Id,
            [TelemetryProperty.Accounts.Name] = server.Name,
            [TelemetryProperty.Accounts.MachineIdentifier] = server.MachineIdentifier,
            [TelemetryProperty.Accounts.IsEnabled] = server.IsEnabled,
            [TelemetryProperty.Accounts.OwnedOverride] = server.OwnedOverride!,
            [TelemetryProperty.Accounts.Owned] = server.Owned,
            [TelemetryProperty.Accounts.PreferredConnectionId] = server.PreferredConnectionId,
        };

    private static Dictionary<string, object> ConnectionProperties(
        PlexServerConnection connection,
        int? preferredConnectionId
    ) =>
        new()
        {
            [TelemetryProperty.Accounts.Id] = connection.Id,
            [TelemetryProperty.Accounts.PlexServerId] = connection.PlexServerId,
            [TelemetryProperty.Accounts.Protocol] = connection.Protocol,
            [TelemetryProperty.Accounts.Address] = connection.Address,
            [TelemetryProperty.Accounts.Port] = connection.Port,
            [TelemetryProperty.Accounts.Url] = connection.Url,
            [TelemetryProperty.Accounts.IsCustom] = connection.IsCustom,
            [TelemetryProperty.Accounts.Local] = connection.Local,
            [TelemetryProperty.Accounts.Relay] = connection.Relay,
            [TelemetryProperty.Accounts.Ipv4] = connection.Ipv4,
            [TelemetryProperty.Accounts.Ipv6] = connection.Ipv6,
            [TelemetryProperty.Accounts.IsPlexTvConnection] = connection.IsPlexTvConnection,
            [TelemetryProperty.Accounts.Type] = connection.Type.ToString(),
            [TelemetryProperty.Accounts.IsPreferred] = preferredConnectionId == connection.Id,
        };
}
