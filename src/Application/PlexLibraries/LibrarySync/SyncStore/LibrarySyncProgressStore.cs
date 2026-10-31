using System.Collections.Concurrent;

namespace Reaparr.Application;

public class LibrarySyncProgressStore : ILibrarySyncProgressStore
{
    private readonly ILogger _log;
    private readonly IProgressHubService _progressHubService;
    private readonly ConcurrentDictionary<int, LibraryProgress> _store = new();

    public LibrarySyncProgressStore(ILogger logger, IProgressHubService progressHubService)
    {
        _log = logger.ForContext<LibrarySyncProgressStore>();
        _progressHubService = progressHubService;
    }

    public LibraryProgress? Get(int plexLibraryId) =>
        _store.TryGetValue(plexLibraryId, out var progress) ? progress : null;

    public async Task StartAsync(
        int plexLibraryId,
        PlexMediaType type,
        IReadOnlyDictionary<PlexMediaType, int>? progressTotals = null
    )
    {
        var items = type.GetFamilyTypes()
            .Select(mediaType => new LibraryProgressItem
            {
                MediaType = mediaType,
                Received = 0,
                Total =
                    progressTotals is not null && progressTotals.TryGetValue(mediaType, out var total) && total >= 0
                        ? total
                        : -1,
                TimeRemaining = TimeSpan.Zero,
            })
            .ToList();

        var progress = new LibraryProgress
        {
            PlexLibraryId = plexLibraryId,
            PlexLibraryType = type,
            Items = items,
        };

        _store[plexLibraryId] = progress;

        // Send initial progress update to clients
        await SendProgressUpdateAsync(progress);
    }

    public async Task UpdateItemAsync(int plexLibraryId, LibraryProgressItem item)
    {
        var updated = _store.AddOrUpdate(
            plexLibraryId,
            _ => throw new InvalidOperationException("Library not initialized."),
            (_, existing) =>
            {
                // Keep the preflight total for this refresh. A per-type retrieval total is
                // only used when the preflight could not determine that layer's total.
                var replaced = false;
                var updatedItems = existing
                    .Items.Select(existingItem =>
                    {
                        if (existingItem.MediaType != item.MediaType)
                            return existingItem;

                        replaced = true;
                        var total = existingItem.Total >= 0 ? existingItem.Total : item.Total;
                        return item with { Total = total };
                    })
                    .ToList();

                if (!replaced)
                    updatedItems.Add(item);

                return existing with
                {
                    Items = updatedItems,
                };
            }
        );

        await SendProgressUpdateAsync(updated);
    }

    public async Task UpdateErrorAsync(int plexLibraryId, Result errorResult)
    {
        var updated = _store.AddOrUpdate(
            plexLibraryId,
            _ => throw new InvalidOperationException("Library not initialized."),
            (_, existing) => existing with { Errors = errorResult.Errors }
        );

        await SendProgressUpdateAsync(updated);

        _store.TryRemove(plexLibraryId, out _);
    }

    private async Task SendProgressUpdateAsync(int plexLibraryId)
    {
        if (!_store.TryGetValue(plexLibraryId, out var progress))
        {
            _log.Here()
                .Warning(
                    "Attempted to send progress update for unknown library with ID {PlexLibraryId}",
                    plexLibraryId
                );

            return;
        }

        await SendProgressUpdateAsync(progress);
    }

    private async Task SendProgressUpdateAsync(LibraryProgress progress)
    {
        var dto = new LibrarySyncProgressDTO
        {
            PlexLibraryId = progress.PlexLibraryId,
            TimeRemaining = progress.TimeRemaining,
            Items = progress
                .Items.Select(x => new LibrarySyncProgressItemDTO
                {
                    MediaType = x.MediaType,
                    Received = x.Received,
                    Total = x.Total,
                    TimeRemaining = x.TimeRemaining,
                })
                .ToList(),
            Errors = progress.Errors,
        };

        await _progressHubService.SendLibraryProgressUpdateAsync(dto);
    }
}
