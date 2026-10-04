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

    public async Task StartAsync(int plexLibraryId, PlexMediaType type, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LibraryProgressItem> items;

        if (type == PlexMediaType.Movie)
        {
            items =
            [
                new LibraryProgressItem
                {
                    MediaType = PlexMediaType.Movie,
                    Received = 0,
                    Total = -1,
                    TimeRemaining = TimeSpan.Zero,
                },
            ];
        }
        else if (type == PlexMediaType.TvShow)
        {
            items =
            [
                new LibraryProgressItem
                {
                    MediaType = PlexMediaType.TvShow,
                    Received = 0,
                    Total = -1,
                    TimeRemaining = TimeSpan.Zero,
                },
                new LibraryProgressItem
                {
                    MediaType = PlexMediaType.Season,
                    Received = 0,
                    Total = -1,
                    TimeRemaining = TimeSpan.Zero,
                },
                new LibraryProgressItem
                {
                    MediaType = PlexMediaType.Episode,
                    Received = 0,
                    Total = -1,
                    TimeRemaining = TimeSpan.Zero,
                },
            ];
        }
        else if (type == PlexMediaType.Music)
        {
            items = new[] { PlexMediaType.Music, PlexMediaType.Album, PlexMediaType.Track }
                .Select(mediaType => new LibraryProgressItem
                {
                    MediaType = mediaType,
                    Received = 0,
                    Total = -1,
                    TimeRemaining = TimeSpan.Zero,
                })
                .ToList();
        }
        else if (type == PlexMediaType.PhotoAlbum)
        {
            items = new[] { PlexMediaType.PhotoAlbum, PlexMediaType.Photos }
                .Select(mediaType => new LibraryProgressItem
                {
                    MediaType = mediaType,
                    Received = 0,
                    Total = -1,
                    TimeRemaining = TimeSpan.Zero,
                })
                .ToList();
        }
        else if (type == PlexMediaType.OtherVideos)
        {
            items = new[] { PlexMediaType.OtherVideos }
                .Select(mediaType => new LibraryProgressItem
                {
                    MediaType = mediaType,
                    Received = 0,
                    Total = -1,
                    TimeRemaining = TimeSpan.Zero,
                })
                .ToList();
        }
        else
        {
            _log.Here()
                .Warning("Unsupported PlexMediaType {PlexMediaType} for library {PlexLibraryId}", type, plexLibraryId);
            return;
        }

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

    public async Task UpdateItemAsync(
        int plexLibraryId,
        LibraryProgressItem item,
        CancellationToken cancellationToken = default
    )
    {
        _store.AddOrUpdate(
            plexLibraryId,
            _ => throw new InvalidOperationException("Library not initialized."),
            (_, existing) =>
            {
                // Upsert: replace if the media type already exists, otherwise append
                var replaced = false;
                var updatedItems = existing
                    .Items.Select(i =>
                    {
                        if (i.MediaType != item.MediaType)
                            return i;

                        replaced = true;
                        return item;
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

        await SendProgressUpdateAsync(plexLibraryId);
    }

    public async Task UpdateErrorAsync(
        int plexLibraryId,
        Result errorResult,
        CancellationToken cancellationToken = default
    )
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
