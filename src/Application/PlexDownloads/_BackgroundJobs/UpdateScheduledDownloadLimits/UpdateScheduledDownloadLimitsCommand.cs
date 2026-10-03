namespace Reaparr.Application;

public record UpdateScheduledDownloadLimitsCommand(DateTimeOffset LocalTime) : ICommand<Result>;

public class UpdateScheduledDownloadLimitsCommandValidator : AbstractValidator<UpdateScheduledDownloadLimitsCommand>
{
    public UpdateScheduledDownloadLimitsCommandValidator()
    {
        RuleFor(x => x.LocalTime).NotEmpty();
    }
}

public class UpdateScheduledDownloadLimitsCommandHandler : ICommandHandler<UpdateScheduledDownloadLimitsCommand, Result>
{
    private readonly ILogger _log;
    private readonly IUserSettings _userSettings;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IDownloadSpeedLimitProvider _speedLimits;

    public UpdateScheduledDownloadLimitsCommandHandler(
        ILogger log,
        IUserSettings userSettings,
        IReaparrDbContextFactory dbContextFactory,
        IDownloadSpeedLimitProvider speedLimits
    )
    {
        _log = log.ForContext<UpdateScheduledDownloadLimitsCommandHandler>();
        _userSettings = userSettings;
        _dbContextFactory = dbContextFactory;
        _speedLimits = speedLimits;
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
        UpdateScheduledDownloadLimitsCommand command,
        CancellationToken cancellationToken
    )
    {
        var schedule = _userSettings.DownloadManagerSettings.DownloadSchedule;
        if (!DownloadSchedule.IsValidDays(schedule.Days))
            return Result.Fail("The saved download schedule contains invalid change points or limits.").LogError();

        _log.Here().Debug("Applying download schedule for local time {LocalTime}", command.LocalTime);
        var result = await Result.Try(async Task () =>
        {
            int? limitKb = null;
            if (schedule.Enabled && schedule.Days.TryGetValue(Enum.GetName(command.LocalTime.DayOfWeek)!, out var points))
            {
                var localMinute = command.LocalTime.Hour * 60 + command.LocalTime.Minute;
                var latestMinute = -1;
                foreach (var (time, limit) in points)
                {
                    var minute = ((time[0] - '0') * 10 + time[1] - '0') * 60 + (time[3] - '0') * 10;
                    if (minute <= localMinute && minute > latestMinute)
                    {
                        limitKb = limit;
                        latestMinute = minute;
                    }
                }
            }

            var allocations = new Dictionary<string, long>(StringComparer.Ordinal);
            if (limitKb.HasValue)
            {
                var budget = checked((long)limitKb.Value * 1024);
                using var dbContext = await _dbContextFactory.CreateAsync();
                var serverIds = await dbContext
                    .DownloadTaskMovieFile.Where(x => x.DownloadStatus == DownloadStatus.Downloading)
                    .Select(x => x.PlexServer!.MachineIdentifier)
                    .Union(
                        dbContext
                            .DownloadTaskTvShowEpisodeFile.Where(x => x.DownloadStatus == DownloadStatus.Downloading)
                            .Select(x => x.PlexServer!.MachineIdentifier)
                    )
                    .ToListAsync(cancellationToken);

                var participants = new List<(string MachineIdentifier, long Cap)>(serverIds.Count);
                foreach (var machineIdentifier in serverIds)
                {
                    // Reading a missing manual setting must not create/persist a settings row.
                    var manualKb =
                        _userSettings
                            .ServerSettings.Data.FirstOrDefault(x => x.MachineIdentifier == machineIdentifier)
                            ?.DownloadSpeedLimit
                        ?? 0;
                    var cap = manualKb > 0 ? checked((long)manualKb * 1024) : long.MaxValue;
                    participants.Add((machineIdentifier, cap));
                }

                participants.Sort(
                    static (a, b) =>
                    {
                        var capOrder = a.Cap.CompareTo(b.Cap);
                        return capOrder != 0
                            ? capOrder
                            : StringComparer.Ordinal.Compare(a.MachineIdentifier, b.MachineIdentifier);
                    }
                );

                var remaining = budget;
                var count = participants.Count;
                foreach (var participant in participants)
                {
                    var share = Math.Min(remaining / count, participant.Cap);

                    // Download engines interpret zero as Unlimited, not as a zero-byte grant.
                    if (share == 0)
                        throw new InvalidOperationException(
                            "The scheduled budget cannot give every downloading server at least one byte per second."
                        );

                    allocations.Add(participant.MachineIdentifier, share);
                    remaining -= share;
                    count--;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            _speedLimits.SetScheduledDownloadSpeedLimits(allocations);
        });

        return result;
    }
}
