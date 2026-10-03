namespace Reaparr.Application.Contracts;

public interface IDownloadSpeedLimitProvider : IDisposable
{
    void SetScheduledDownloadSpeedLimits(IReadOnlyDictionary<string, long> limits);

    long GetEffectiveDownloadSpeedLimit(string machineIdentifier);

    IObservable<long> GetEffectiveDownloadSpeedLimitObservable(string machineIdentifier);
}
