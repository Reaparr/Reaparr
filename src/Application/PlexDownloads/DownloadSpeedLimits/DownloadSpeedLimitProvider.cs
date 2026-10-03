using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Reaparr.Application;

public sealed class DownloadSpeedLimitProvider : IDownloadSpeedLimitProvider
{
    private readonly BehaviorSubject<IReadOnlyDictionary<string, long>> _scheduledLimits = new(
        new Dictionary<string, long>()
    );

    private readonly IUserSettings _userSettings;

    public DownloadSpeedLimitProvider(IUserSettings userSettings)
    {
        _userSettings = userSettings;
    }

    public void SetScheduledDownloadSpeedLimits(IReadOnlyDictionary<string, long> limits)
    {
        foreach (var limit in limits.Values)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        _scheduledLimits.OnNext(new Dictionary<string, long>(limits, StringComparer.Ordinal));
    }

    public long GetEffectiveDownloadSpeedLimit(string machineIdentifier) =>
        GetEffectiveLimit(machineIdentifier, _scheduledLimits.Value);

    public IObservable<long> GetEffectiveDownloadSpeedLimitObservable(string machineIdentifier) =>
        _userSettings
            .ServerSettings.HasChanged.CombineLatest(
                _scheduledLimits,
                (_, limits) => GetEffectiveLimit(machineIdentifier, limits)
            )
            .DistinctUntilChanged();

    private long GetEffectiveLimit(string machineIdentifier, IReadOnlyDictionary<string, long> scheduledLimits)
    {
        var manualLimit =
            _userSettings
                .ServerSettings.Data.FirstOrDefault(x => x.MachineIdentifier == machineIdentifier)
                ?.DownloadSpeedLimit
            ?? 0;
        var manualBytes = manualLimit > 0 ? (long)manualLimit * 1024 : 0;
        if (!scheduledLimits.TryGetValue(machineIdentifier, out var scheduledBytes))
            return manualBytes;

        return manualBytes == 0 ? scheduledBytes : Math.Min(manualBytes, scheduledBytes);
    }

    public void Dispose() => _scheduledLimits.Dispose();
}
