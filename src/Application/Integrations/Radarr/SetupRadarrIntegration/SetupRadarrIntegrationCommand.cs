namespace Reaparr.Application;

public record SetupRadarrIntegrationCommand(Guid IntegrationId) : ICommand<Result<RadarrIntegration>>;

public class SetupRadarrIntegrationCommandValidator : AbstractValidator<SetupRadarrIntegrationCommand>
{
    public SetupRadarrIntegrationCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.IntegrationId).NotEmpty();
    }
}

public class SetupRadarrIntegrationCommandHandler
    : ICommandHandler<SetupRadarrIntegrationCommand, Result<RadarrIntegration>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IProgressHubService _progressHubService;
    private readonly ITelemetry _telemetry;

    public SetupRadarrIntegrationCommandHandler(
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        IProgressHubService progressHubService,
        ITelemetry telemetry
    )
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _progressHubService = progressHubService;
        _telemetry = telemetry;
    }

    public async Task<Result<RadarrIntegration>> ExecuteAsync(
        SetupRadarrIntegrationCommand command,
        CancellationToken ct
    )
    {
        var integration = await _dbContext
            .RadarrIntegrations.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == command.IntegrationId, ct);
        if (integration is null)
            return ResultExtensions.EntityNotFound(nameof(RadarrIntegration), command.IntegrationId);

        var downloadClientResult = await _commandExecutor.Send(
            new SetupRadarrDownloadClientCommand { IntegrationId = integration.Id },
            ct
        );
        if (downloadClientResult.IsCancelled)
        {
            var error = string.Join("; ", downloadClientResult.Errors.Select(x => x.Message));
            await SendProgress(integration.Id, IntegrationSetupProgressStage.Connecting, false, error: error);
            await SendProgress(integration.Id, IntegrationSetupProgressStage.DownloadClient, false, error: error);
            _telemetry.IntegrationSetupFinished(
                integration,
                downloadClientResult,
                IntegrationSetupProgressStage.DownloadClient
            );
            return downloadClientResult.ToResult<RadarrIntegration>().LogWarning();
        }

        if (downloadClientResult.IsFailed)
        {
            var error = string.Join("; ", downloadClientResult.Errors.Select(x => x.Message));
            await SendProgress(integration.Id, IntegrationSetupProgressStage.Connecting, false, error: error);
            await SendProgress(integration.Id, IntegrationSetupProgressStage.DownloadClient, false, error: error);
            _telemetry.IntegrationSetupFinished(
                integration,
                downloadClientResult,
                IntegrationSetupProgressStage.DownloadClient
            );
            _telemetry.CaptureErrors(downloadClientResult, TelemetryActivity.IntegrationSetup);
            return downloadClientResult.ToResult<RadarrIntegration>().LogError();
        }

        await SendProgress(integration.Id, IntegrationSetupProgressStage.DownloadClient, true);
        integration.ExternalDownloadClientId = downloadClientResult.Value.DownloadClientId;
        await _dbContext.SaveChangesAsync(ct);

        await SendProgress(integration.Id, IntegrationSetupProgressStage.Indexer, false, true);
        var indexerResult = await _commandExecutor.Send(
            new SetupRadarrIndexerCommand
            {
                IntegrationId = integration.Id,
                DownloadClientId = downloadClientResult.Value.DownloadClientId,
            },
            ct
        );
        if (indexerResult.IsCancelled)
        {
            await SendProgress(
                integration.Id,
                IntegrationSetupProgressStage.Indexer,
                false,
                error: string.Join("; ", indexerResult.Errors.Select(x => x.Message))
            );
            _telemetry.IntegrationSetupFinished(integration, indexerResult, IntegrationSetupProgressStage.Indexer);
            return indexerResult.ToResult<RadarrIntegration>().LogWarning();
        }

        if (indexerResult.IsFailed)
        {
            await SendProgress(
                integration.Id,
                IntegrationSetupProgressStage.Indexer,
                false,
                error: string.Join("; ", indexerResult.Errors.Select(x => x.Message))
            );
            _telemetry.IntegrationSetupFinished(integration, indexerResult, IntegrationSetupProgressStage.Indexer);
            _telemetry.CaptureErrors(indexerResult, TelemetryActivity.IntegrationSetup);
            return indexerResult.ToResult<RadarrIntegration>().LogError();
        }

        await SendProgress(integration.Id, IntegrationSetupProgressStage.Indexer, true);
        integration.ExternalIndexerId = indexerResult.Value.IndexerId;
        await _dbContext.SaveChangesAsync(ct);

        await SendProgress(integration.Id, IntegrationSetupProgressStage.Validation, false, true);
        var validationResult = await _commandExecutor.Send(
            new ValidateRadarrIntegrationCommand(
                integration.Id,
                downloadClientResult.Value.Resource,
                indexerResult.Value.Resource
            ),
            ct
        );
        if (validationResult.IsCancelled)
        {
            await SendProgress(
                integration.Id,
                IntegrationSetupProgressStage.Validation,
                false,
                error: string.Join("; ", validationResult.Errors.Select(x => x.Message))
            );
            _telemetry.IntegrationSetupFinished(
                integration,
                validationResult,
                IntegrationSetupProgressStage.Validation
            );
            return validationResult.ToResult<RadarrIntegration>().LogWarning();
        }

        if (validationResult.IsFailed)
        {
            await SendProgress(
                integration.Id,
                IntegrationSetupProgressStage.Validation,
                false,
                error: string.Join("; ", validationResult.Errors.Select(x => x.Message))
            );
            _telemetry.IntegrationSetupFinished(
                integration,
                validationResult,
                IntegrationSetupProgressStage.Validation
            );
            _telemetry.CaptureErrors(validationResult, TelemetryActivity.IntegrationSetup);
            return validationResult.ToResult<RadarrIntegration>().LogError();
        }

        await SendProgress(integration.Id, IntegrationSetupProgressStage.Validation, true);
        integration.ProvisioningState = IntegrationProvisioningState.Configured;
        integration.LastConnectionTestStatus = TestConnectionStatus.Success;
        integration.LastConnectionTestHttpStatusCode = StatusCodes.Status200OK;
        integration.LastConnectionTestErrorMessage = null;
        integration.LastConnectionTestedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        _telemetry.IntegrationSetupFinished(integration, Result.Ok(integration), IntegrationSetupProgressStage.Done);
        await SendProgress(integration.Id, IntegrationSetupProgressStage.Done, true);
        return Result.Ok(integration);
    }

    private Task SendProgress(
        Guid integrationId,
        IntegrationSetupProgressStage stage,
        bool isSuccess,
        bool isRunning = false,
        string? error = null
    ) =>
        _progressHubService.SendIntegrationSetupProgressAsync(
            new IntegrationSetupProgressDTO
            {
                IntegrationId = integrationId,
                Stage = stage,
                IsRunning = isRunning,
                IsSuccess = isSuccess,
                Error = error,
            }
        );
}
