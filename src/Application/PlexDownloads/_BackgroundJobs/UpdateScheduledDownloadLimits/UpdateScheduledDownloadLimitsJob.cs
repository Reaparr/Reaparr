namespace Reaparr.Application;

/// <summary>
/// Dispatches scheduled download limit recalculation.
/// </summary>
[DisallowConcurrentExecution]
public sealed class UpdateScheduledDownloadLimitsJob : IJob
{
    private readonly ICommandExecutor _commandExecutor;

    public UpdateScheduledDownloadLimitsJob(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public static JobKey GetJobKey() =>
        new(nameof(UpdateScheduledDownloadLimitsJob), nameof(UpdateScheduledDownloadLimitsJob));

    public static TriggerKey GetTriggerKey() =>
        new(nameof(UpdateScheduledDownloadLimitsJob), nameof(UpdateScheduledDownloadLimitsJob));

    public static ITrigger CreateTrigger(string timeZoneId) =>
        TriggerBuilder
            .Create()
            .WithIdentity(GetTriggerKey())
            .ForJob(GetJobKey())
            .WithCronSchedule(
                "0 0,30 * * * ?",
                x =>
                    x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId))
                        .WithMisfireHandlingInstructionFireAndProceed()
            )
            .Build();

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        var result = await _commandExecutor.Send(new UpdateScheduledDownloadLimitsCommand(), context.CancellationToken);

        var status =
            result.IsCancelled ? JobStatus.Cancelled
            : result.IsFailed ? JobStatus.Failed
            : JobStatus.Completed;
        result.LogIfFailed();
        context.SetResult(status, result);
    }
}
