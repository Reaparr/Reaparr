namespace Reaparr.Application.Contracts;

public interface ILibrarySyncProgressStore
{
    LibraryProgress? Get(int plexLibraryId);

    Task StartAsync(
        int plexLibraryId,
        PlexMediaType type,
        IReadOnlyDictionary<PlexMediaType, int>? progressTotals = null
    );

    Task UpdateItemAsync(int plexLibraryId, LibraryProgressItem item);

    Task UpdateErrorAsync(int plexLibraryId, Result errorResult);
}
