namespace Reaparr.Data.Contracts;

public static partial class DbSetExtensions
{
    /// <summary>
    /// Resets completed, failed, or cancelled library sync jobs to queued for the specified library IDs.
    /// </summary>
    public static async Task<List<LibrarySyncJobQueue>> ResetLibrarySyncJobQueue(
        this IQueryable<LibrarySyncJobQueue> dbSet,
        List<int> libraryIds,
        CancellationToken token = default
    )
    {
        var resetCreatedAt = DateTime.UtcNow;
        var resetCount = await dbSet
            .Where(x =>
                libraryIds.Contains(x.PlexLibraryId)
                && (
                    x.Status == LibrarySyncJobStatus.Cancelled
                    || x.Status == LibrarySyncJobStatus.Failed
                    || x.Status == LibrarySyncJobStatus.Completed
                )
            )
            .ResetJobsToQueuedAsync(token, createdAt: resetCreatedAt);

        if (resetCount == 0)
            return [];

        return await dbSet
            .Where(x => libraryIds.Contains(x.PlexLibraryId) && x.CreatedAt == resetCreatedAt)
            .ToListAsync(CancellationToken.None);
    }

    public static async Task<int> ResetJobsToQueuedAsync(
        this IQueryable<LibrarySyncJobQueue> queryable,
        CancellationToken token = default,
        DateTime? createdAt = null
    )
    {
        var resetCreatedAt = createdAt ?? DateTime.UtcNow;
        return await queryable.ExecuteUpdateAsync(
            x =>
                x.SetProperty(y => y.Status, LibrarySyncJobStatus.Queued)
                    .SetProperty(y => y.CreatedAt, resetCreatedAt)
                    .SetProperty(y => y.StartedAt, (DateTime?)null)
                    .SetProperty(y => y.CompletedAt, (DateTime?)null)
                    .SetProperty(y => y.ErrorMessage, (string?)null)
                    .SetProperty(y => y.IsServerOffline, false),
            token
        );
    }
}
