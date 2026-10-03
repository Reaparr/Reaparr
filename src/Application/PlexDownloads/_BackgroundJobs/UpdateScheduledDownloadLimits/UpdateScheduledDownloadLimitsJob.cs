namespace Reaparr.Application;

/// <summary>
/// Resolves the configured local clock and dispatches schedule recalculation.
/// </summary>
[DisallowConcurrentExecution]
public sealed class UpdateScheduledDownloadLimitsJob : IJob
{
    private readonly IUserSettings _userSettings;
    private readonly ICommandExecutor _commandExecutor;
    private readonly TimeProvider _timeProvider;

    public UpdateScheduledDownloadLimitsJob(
        IUserSettings userSettings,
        ICommandExecutor commandExecutor,
        TimeProvider timeProvider
    )
    {
        _userSettings = userSettings;
        _commandExecutor = commandExecutor;
        _timeProvider = timeProvider;
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
        var result = await Result.Try(async Task<Result> () =>
        {
            var now = _timeProvider.GetUtcNow();
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_userSettings.DateTimeSettings.TimeZone);
            var localTime = TimeZoneInfo.ConvertTime(now, timeZone);
            return await _commandExecutor.Send(
                new UpdateScheduledDownloadLimitsCommand(localTime),
                context.CancellationToken
            );
        });

        var status =
            result.IsCancelled ? JobStatus.Cancelled
            : result.IsFailed ? JobStatus.Failed
            : JobStatus.Completed;
        result.LogIfFailed();
        context.SetResult(status, result);
    }
}
