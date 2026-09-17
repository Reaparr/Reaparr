namespace Reaparr.Application;

public record ScheduleOptimizeDatabaseJobCommand : ICommand<Result>
{
    public int ChangedItemCount { get; init; }
}

public class ScheduleOptimizeDatabaseJobCommandValidator : AbstractValidator<ScheduleOptimizeDatabaseJobCommand>
{
    public ScheduleOptimizeDatabaseJobCommandValidator()
    {
        RuleFor(x => x).NotNull();
    }
}

public class ScheduleOptimizeDatabaseJobCommandHandler : ICommandHandler<ScheduleOptimizeDatabaseJobCommand, Result>
{
    private readonly IScheduler _scheduler;

    internal static readonly TimeSpan DebounceDelay = TimeSpan.FromMinutes(1);
    internal const int LargeLibraryChangeThreshold = 1_000;

    public ScheduleOptimizeDatabaseJobCommandHandler(IScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(
        ScheduleOptimizeDatabaseJobCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.ChangedItemCount < LargeLibraryChangeThreshold)
            return Result.Ok();

        return await Result.Try(async Task () =>
        {
            var jobKey = OptimizeDatabaseJob.GetJobKey();
            var job = JobBuilder
                .Create<OptimizeDatabaseJob>()
                .WithIdentity(jobKey)
                .DisallowConcurrentExecution()
                .RequestRecovery()
                .Build();

            var trigger = TriggerBuilder
                .Create()
                .WithIdentity(jobKey.Name, jobKey.Group)
                .ForJob(jobKey)
                .StartAt(DateTimeOffset.UtcNow.Add(DebounceDelay))
                .WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow())
                .Build();

            await _scheduler.ScheduleJobs(
                new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>> { [job] = [trigger] },
                replace: true,
                CancellationToken.None
            );
        });
    }
}
