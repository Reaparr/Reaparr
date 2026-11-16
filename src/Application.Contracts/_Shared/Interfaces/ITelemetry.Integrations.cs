namespace Reaparr.Application.Contracts;

public partial interface ITelemetry
{
    /// <summary>
    /// Records an integration change.
    /// </summary>
    void IntegrationChanged(SonarrIntegration integration, TelemetryAction action, bool connectionIdentityChanged);

    /// <summary>
    /// Records an integration change.
    /// </summary>
    void IntegrationChanged(RadarrIntegration integration, TelemetryAction action, bool connectionIdentityChanged);

    /// <summary>
    /// Records deletion of an integration.
    /// </summary>
    void IntegrationDeleted(
        SonarrIntegration integration,
        ResultBase remoteCleanupResult,
        bool localRemoved,
        bool forced
    );

    /// <summary>
    /// Records deletion of an integration.
    /// </summary>
    void IntegrationDeleted(
        RadarrIntegration integration,
        ResultBase remoteCleanupResult,
        bool localRemoved,
        bool forced
    );

    /// <summary>
    /// Records completion of integration setup.
    /// </summary>
    void IntegrationSetupFinished<TStage>(SonarrIntegration integration, ResultBase result, TStage stage)
        where TStage : struct, Enum;

    /// <summary>
    /// Records completion of integration setup.
    /// </summary>
    void IntegrationSetupFinished<TStage>(RadarrIntegration integration, ResultBase result, TStage stage)
        where TStage : struct, Enum;

    /// <summary>
    /// Records completion of an integration connection test.
    /// </summary>
    void IntegrationConnectionTestFinished(
        IntegrationType provider,
        Guid? integrationId,
        ConnectionTestTelemetryDTO? report,
        ResultBase result
    );

    /// <summary>
    /// Records completion of an external indexer query.
    /// </summary>
    void ExternalIndexerQueryFinished(ExternalIndexerQueryReport report, ResultBase result);

    /// <summary>
    /// Records generation of torrent metadata.
    /// </summary>
    void TorrentMetadataGenerated(
        IntegrationIdentity integration,
        TorrentMetadataTelemetryDTO metadata,
        string fileName,
        long fileSize,
        int pieceLength,
        int pieceCount
    );

    /// <summary>
    /// Records processing of an external download submission.
    /// </summary>
    void ExternalDownloadSubmissionProcessed(
        IntegrationIdentity integration,
        TorrentMetadataTelemetryDTO metadata,
        string hashId,
        Result<DownloadTaskCreationReport>? creationResult,
        Result<int>? associationResult,
        TelemetryStage stage
    );

    /// <summary>
    /// Records processing of an external download removal.
    /// </summary>
    void ExternalDownloadRemovalProcessed(IntegrationIdentity integration, ExternalDownloadRemovalReport report);
}
