namespace Reaparr.Application;

public sealed partial class Telemetry
{
    /// <inheritdoc/>
    public void IntegrationChanged(
        SonarrIntegration integration,
        TelemetryAction action,
        bool connectionIdentityChanged
    ) => CaptureIntegrationChanged(CreateIntegrationProperties(integration), action, connectionIdentityChanged);

    /// <inheritdoc/>
    public void IntegrationChanged(
        RadarrIntegration integration,
        TelemetryAction action,
        bool connectionIdentityChanged
    ) => CaptureIntegrationChanged(CreateIntegrationProperties(integration), action, connectionIdentityChanged);

    /// <inheritdoc/>
    public void IntegrationDeleted(
        SonarrIntegration integration,
        ResultBase remoteCleanupResult,
        bool localRemoved,
        bool forced
    ) =>
        CaptureIntegrationDeleted(
            CreateIntegrationProperties(integration),
            remoteCleanupResult,
            integration.ExternalDownloadClientId.HasValue || integration.ExternalIndexerId.HasValue,
            localRemoved,
            forced
        );

    /// <inheritdoc/>
    public void IntegrationDeleted(
        RadarrIntegration integration,
        ResultBase remoteCleanupResult,
        bool localRemoved,
        bool forced
    ) =>
        CaptureIntegrationDeleted(
            CreateIntegrationProperties(integration),
            remoteCleanupResult,
            integration.ExternalDownloadClientId.HasValue || integration.ExternalIndexerId.HasValue,
            localRemoved,
            forced
        );

    /// <inheritdoc/>
    public void IntegrationSetupFinished<TStage>(SonarrIntegration integration, ResultBase result, TStage stage)
        where TStage : struct, Enum =>
        CaptureIntegrationSetupFinished(CreateIntegrationProperties(integration), result, stage.ToString());

    /// <inheritdoc/>
    public void IntegrationSetupFinished<TStage>(RadarrIntegration integration, ResultBase result, TStage stage)
        where TStage : struct, Enum =>
        CaptureIntegrationSetupFinished(CreateIntegrationProperties(integration), result, stage.ToString());

    /// <inheritdoc/>
    public void IntegrationConnectionTestFinished(
        IntegrationType provider,
        Guid? integrationId,
        ConnectionTestTelemetryDTO? report,
        ResultBase result
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Provider] = provider.ToString(),
            [TelemetryProperty.Mode] = (
                integrationId.HasValue ? TelemetryMode.Stored : TelemetryMode.Explicit
            ).ToString(),
            [TelemetryProperty.Outcome] = Outcome(result),
        };
        if (integrationId.HasValue)
            properties[TelemetryProperty.IntegrationId] = integrationId.Value;
        if (result.IsSuccess && report is not null)
        {
            properties[TelemetryProperty.Outcome] = (
                report.Status switch
                {
                    TestConnectionStatus.Success => TelemetryOutcome.Succeeded,
                    TestConnectionStatus.InvalidApiKey or TestConnectionStatus.UrlIsInvalid =>
                        TelemetryOutcome.Rejected,
                    _ => TelemetryOutcome.Failed,
                }
            ).ToString();
            properties[TelemetryProperty.Status] = report.Status.ToString();
            if (report.HttpStatusCode.HasValue)
                properties[TelemetryProperty.HttpStatusCode] = report.HttpStatusCode.Value;
        }

        _ = Capture(TelemetryEvent.IntegrationConnectionTestFinished, properties);
    }

    /// <inheritdoc/>
    public void ExternalIndexerQueryFinished(ExternalIndexerQueryReport report, ResultBase result)
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Mode] = report.Mode.ToString(),
            [TelemetryProperty.Integrations.QueryType] = report.Type.ToString(),
            [TelemetryProperty.Provider] = report.Integration.Type.ToString(),
            [TelemetryProperty.IntegrationId] = report.Integration.Id,
            [TelemetryProperty.Query] = report.Query,
            [TelemetryProperty.Integrations.ImdbId] = report.ImdbId,
            [TelemetryProperty.Integrations.TmdbId] = report.TmdbId,
            [TelemetryProperty.Integrations.TvdbId] = report.TvdbId,
            [TelemetryProperty.Integrations.Season] = report.Season,
            [TelemetryProperty.Integrations.Episode] = report.Episode,
            [TelemetryProperty.Integrations.Categories] = report.Categories,
            [TelemetryProperty.Outcome] = Outcome(result),
        };
        if (result.IsSuccess)
        {
            properties[TelemetryProperty.ReturnedItemCount] = report.ReturnedItemCount;
            properties[TelemetryProperty.TotalCount] = report.TotalCount;
        }

        _ = Capture(TelemetryEvent.ExternalIndexerQueryFinished, properties);
    }

    /// <inheritdoc/>
    public void TorrentMetadataGenerated(
        IntegrationIdentity integration,
        TorrentMetadataTelemetryDTO metadata,
        string fileName,
        long fileSize,
        int pieceLength,
        int pieceCount
    )
    {
        _ = Capture(
            TelemetryEvent.TorrentMetadataGenerated,
            new Dictionary<string, object>
            {
                [TelemetryProperty.IntegrationId] = integration.Id,
                [TelemetryProperty.Provider] = integration.Type.ToString(),
                [TelemetryProperty.MediaType] = metadata.Type.ToString(),
                [TelemetryProperty.ServerId] = metadata.ServerId,
                [TelemetryProperty.LibraryId] = metadata.LibraryId,
                [TelemetryProperty.MediaId] = metadata.MediaId,
                [TelemetryProperty.DataId] = metadata.DataId,
                [TelemetryProperty.PartId] = metadata.PartId,
                [TelemetryProperty.PlexApiPartId] = metadata.PlexApiPartId,
                [TelemetryProperty.Integrations.VideoQuality] = metadata.VideoQuality.ToString(),
                [TelemetryProperty.FileName] = fileName,
                [TelemetryProperty.FileSize] = fileSize,
                [TelemetryProperty.Integrations.PieceLength] = pieceLength,
                [TelemetryProperty.Integrations.PieceCount] = pieceCount,
            }
        );
    }

    /// <inheritdoc/>
    public void ExternalDownloadSubmissionProcessed(
        IntegrationIdentity integration,
        TorrentMetadataTelemetryDTO metadata,
        string hashId,
        Result<DownloadTaskCreationReport>? creationResult,
        Result<int>? associationResult,
        TelemetryStage stage
    )
    {
        var creationReport = creationResult?.ValueOrDefault;
        var associatedTaskCount = associationResult?.ValueOrDefault ?? 0;
        var outcome =
            creationResult is null ? TelemetryOutcome.Rejected
            : !creationResult.IsSuccess ? SubmissionOutcome(creationResult)
            : associationResult?.IsCancelledOrFailed == true
                ? creationReport is { Total: > 0 } ? TelemetryOutcome.Partial
                    : SubmissionOutcome(associationResult)
            : associatedTaskCount == 0
                ? creationReport is { Total: > 0 } ? TelemetryOutcome.Partial
                    : TelemetryOutcome.Rejected
            : TelemetryOutcome.Succeeded;
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.IntegrationId] = integration.Id,
            [TelemetryProperty.Provider] = integration.Type.ToString(),
            [TelemetryProperty.MediaType] = metadata.Type.ToString(),
            [TelemetryProperty.ServerId] = metadata.ServerId,
            [TelemetryProperty.LibraryId] = metadata.LibraryId,
            [TelemetryProperty.MediaId] = metadata.MediaId,
            [TelemetryProperty.DataId] = metadata.DataId,
            [TelemetryProperty.PartId] = metadata.PartId,
            [TelemetryProperty.PlexApiPartId] = metadata.PlexApiPartId,
            [TelemetryProperty.Integrations.VideoQuality] = metadata.VideoQuality.ToString(),
            [TelemetryProperty.HashId] = hashId,
            [TelemetryProperty.Outcome] = outcome.ToString(),
            [TelemetryProperty.Stage] = stage.ToString(),
            [TelemetryProperty.Integrations.AssociatedTaskCount] = associatedTaskCount,
        };
        if (creationReport is not null)
        {
            properties[TelemetryProperty.Integrations.CreatedTotal] = creationReport.Total;
            properties[TelemetryProperty.Integrations.CreatedReport] = new Dictionary<string, object>
            {
                [TelemetryProperty.Integrations.CreatedMovies] = creationReport.Movies,
                [TelemetryProperty.Integrations.CreatedTvShows] = creationReport.TvShows,
                [TelemetryProperty.Integrations.CreatedSeasons] = creationReport.Seasons,
                [TelemetryProperty.Integrations.CreatedEpisodes] = creationReport.Episodes,
                [TelemetryProperty.Integrations.CreatedPhotoFolders] = creationReport.PhotoFolders,
                [TelemetryProperty.Integrations.CreatedPhotoImages] = creationReport.PhotoImages,
                [TelemetryProperty.Integrations.CreatedMusicArtists] = creationReport.MusicArtists,
                [TelemetryProperty.Integrations.CreatedMusicAlbums] = creationReport.MusicAlbums,
                [TelemetryProperty.Integrations.CreatedMusicTracks] = creationReport.MusicTracks,
                [TelemetryProperty.Integrations.CreatedOtherVideos] = creationReport.OtherVideos,
            };
        }

        _ = Capture(TelemetryEvent.ExternalDownloadSubmissionProcessed, properties);
    }

    private static TelemetryOutcome SubmissionOutcome(ResultBase result) =>
        result.IsCancelled ? TelemetryOutcome.Cancelled
        : result.HasError<ExceptionalError>() ? TelemetryOutcome.Failed
        : TelemetryOutcome.Rejected;

    /// <inheritdoc/>
    public void ExternalDownloadRemovalProcessed(IntegrationIdentity integration, ExternalDownloadRemovalReport report)
    {
        _ = Capture(
            TelemetryEvent.ExternalDownloadRemovalProcessed,
            new Dictionary<string, object>
            {
                [TelemetryProperty.IntegrationId] = integration.Id,
                [TelemetryProperty.Provider] = integration.Type.ToString(),
                [TelemetryProperty.Outcome] = RemovalOutcome(report).ToString(),
                [TelemetryProperty.Stage] = report.Stage.ToString(),
                [TelemetryProperty.Integrations.RequestedHashCount] = report.RequestedHashCount,
                [TelemetryProperty.Integrations.AllRequested] = report.AllRequested,
                [TelemetryProperty.MatchedCount] = report.MatchedCount,
                [TelemetryProperty.Integrations.ActiveCount] = report.ActiveCount,
                [TelemetryProperty.Integrations.NonActiveCount] = report.NonActiveCount,
                [TelemetryProperty.Integrations.EligibleCount] = report.EligibleCount,
                [TelemetryProperty.DeletedCount] = report.DeletedCount,
                [TelemetryProperty.DeleteFiles] = report.DeleteFiles,
                [TelemetryProperty.Integrations.StopFailureCount] = report.StopFailureCount,
                [TelemetryProperty.Integrations.StopCancellationCount] = report.StopCancellationCount,
                [TelemetryProperty.Integrations.FileFailureCount] = report.FileFailureCount,
                [TelemetryProperty.Integrations.FileCancellationCount] = report.FileCancellationCount,
                [TelemetryProperty.Integrations.CleanupFailureCount] = report.CleanupFailureCount,
            }
        );
    }

    private static TelemetryOutcome RemovalOutcome(ExternalDownloadRemovalReport report)
    {
        var hasPrerequisiteFailures = report.StopFailureCount > 0 || report.FileFailureCount > 0;
        var hasPrerequisiteCancellations = report.StopCancellationCount > 0 || report.FileCancellationCount > 0;
        if (report.Stage is TelemetryStage.Request or TelemetryStage.Lookup)
            return TelemetryOutcome.NoOp;
        if (report.Stage == TelemetryStage.Prerequisites)
        {
            if (report.DeletedCount > 0)
                return TelemetryOutcome.Partial;
            if (hasPrerequisiteCancellations)
                return hasPrerequisiteFailures ? TelemetryOutcome.Partial : TelemetryOutcome.Cancelled;
            return TelemetryOutcome.Failed;
        }
        if (report.CleanupFailureCount > 0 || hasPrerequisiteFailures || hasPrerequisiteCancellations)
            return TelemetryOutcome.Partial;
        if (report.Result is not null && report.Result.IsCancelled)
            return TelemetryOutcome.Cancelled;
        if (report.Result is { IsFailed: true })
            return TelemetryOutcome.Failed;
        return report.DeletedCount > 0 ? TelemetryOutcome.Succeeded : TelemetryOutcome.Failed;
    }

    private void CaptureIntegrationChanged(
        Dictionary<string, object> properties,
        TelemetryAction action,
        bool connectionIdentityChanged
    )
    {
        properties[TelemetryProperty.Action] = action.ToString();
        properties[TelemetryProperty.Integrations.ConnectionIdentityChanged] = connectionIdentityChanged;
        _ = Capture(TelemetryEvent.IntegrationChanged, properties);
    }

    private void CaptureIntegrationDeleted(
        Dictionary<string, object> properties,
        ResultBase remoteCleanupResult,
        bool hasRemoteResources,
        bool localRemoved,
        bool forced
    )
    {
        var remoteCleanupOutcome =
            remoteCleanupResult.IsCancelled ? TelemetryOutcome.Cancelled
            : remoteCleanupResult.IsFailed ? TelemetryOutcome.Failed
            : hasRemoteResources ? TelemetryOutcome.Succeeded
            : TelemetryOutcome.NotRequired;
        var outcome = localRemoved
            ? remoteCleanupOutcome is TelemetryOutcome.Succeeded or TelemetryOutcome.NotRequired
                ? TelemetryOutcome.Succeeded
                : TelemetryOutcome.Partial
            : remoteCleanupOutcome;
        properties[TelemetryProperty.Action] = TelemetryAction.Delete.ToString();
        properties[TelemetryProperty.Outcome] = outcome.ToString();
        properties[TelemetryProperty.Integrations.RemoteCleanupOutcome] = remoteCleanupOutcome.ToString();
        properties[TelemetryProperty.Integrations.LocalRemoved] = localRemoved;
        properties[TelemetryProperty.Integrations.Forced] = forced;
        _ = Capture(TelemetryEvent.IntegrationChanged, properties);
    }

    private void CaptureIntegrationSetupFinished(Dictionary<string, object> properties, ResultBase result, string stage)
    {
        properties[TelemetryProperty.Outcome] = (
            result.IsCancelled ? TelemetryOutcome.Cancelled
            : result.IsFailed ? TelemetryOutcome.Failed
            : TelemetryOutcome.Succeeded
        ).ToString();
        properties[TelemetryProperty.Stage] = stage;
        _ = Capture(TelemetryEvent.IntegrationSetupFinished, properties);
    }

    private static Dictionary<string, object> CreateIntegrationProperties(SonarrIntegration integration)
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.IntegrationId] = integration.Id,
            [TelemetryProperty.Provider] = IntegrationType.Sonarr.ToString(),
            [TelemetryProperty.Configuration.Name] = integration.DisplayName,
            [TelemetryProperty.Configuration.Url] = integration.BaseUrl,
            [TelemetryProperty.Configuration.Category] = integration.Category,
            [TelemetryProperty.Configuration.DownloadFolderId] = integration.DownloadFolderId,
            [TelemetryProperty.Configuration.ProvisioningState] = integration.ProvisioningState.ToString(),
        };
        if (integration.ExternalDownloadClientId.HasValue)
            properties[TelemetryProperty.Configuration.ExternalDownloadClientId] = integration
                .ExternalDownloadClientId
                .Value;
        if (integration.ExternalIndexerId.HasValue)
            properties[TelemetryProperty.Configuration.ExternalIndexerId] = integration.ExternalIndexerId.Value;
        return properties;
    }

    private static Dictionary<string, object> CreateIntegrationProperties(RadarrIntegration integration)
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.IntegrationId] = integration.Id,
            [TelemetryProperty.Provider] = IntegrationType.Radarr.ToString(),
            [TelemetryProperty.Configuration.Name] = integration.DisplayName,
            [TelemetryProperty.Configuration.Url] = integration.BaseUrl,
            [TelemetryProperty.Configuration.Category] = integration.Category,
            [TelemetryProperty.Configuration.DownloadFolderId] = integration.DownloadFolderId,
            [TelemetryProperty.Configuration.ProvisioningState] = integration.ProvisioningState.ToString(),
        };
        if (integration.ExternalDownloadClientId.HasValue)
            properties[TelemetryProperty.Configuration.ExternalDownloadClientId] = integration
                .ExternalDownloadClientId
                .Value;
        if (integration.ExternalIndexerId.HasValue)
            properties[TelemetryProperty.Configuration.ExternalIndexerId] = integration.ExternalIndexerId.Value;
        return properties;
    }
}
