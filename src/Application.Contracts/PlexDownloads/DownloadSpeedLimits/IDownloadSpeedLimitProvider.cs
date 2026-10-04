namespace Reaparr.Application.Contracts;

public interface IDownloadSpeedLimitProvider : IDisposable
{
    /// <summary>
    /// Copies and replaces scheduled allocations without changing saved manual caps. An empty map clears them.
    /// </summary>
    /// <param name="limits">Positive limits in bytes per second, keyed by Plex server machine identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">An allocation is zero or negative.</exception>
    void SetScheduledDownloadSpeedLimits(IReadOnlyDictionary<string, long> limits);

    /// <summary>
    /// Gets the lower finite manual or scheduled limit for a server.
    /// </summary>
    /// <param name="machineIdentifier">The Plex server machine identifier.</param>
    /// <returns>The effective limit in bytes per second, or zero for Unlimited.</returns>
    long GetEffectiveDownloadSpeedLimit(string machineIdentifier);

    /// <summary>
    /// Observes effective limit changes caused by manual caps or scheduled allocations.
    /// </summary>
    /// <param name="machineIdentifier">The Plex server machine identifier.</param>
    /// <returns>Distinct limits in bytes per second, with zero representing Unlimited.</returns>
    IObservable<long> GetEffectiveDownloadSpeedLimitObservable(string machineIdentifier);
}
