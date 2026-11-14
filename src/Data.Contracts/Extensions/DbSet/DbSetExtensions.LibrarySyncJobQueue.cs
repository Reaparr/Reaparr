namespace Reaparr.Data.Contracts;

public static partial class DbSetExtensions
{
    /// <summary>
    /// Resets completed, failed, or cancelled library sync jobs to queued for the specified library IDs.
    /// </summary>
    public static async Task<int> ResetLibrarySyncJobQueue(
        this IQueryable<LibrarySyncJobQueue> dbSet,
        List<int> libraryIds,
        CancellationToken token = default
    )
    {
        return await dbSet
            .Where(x =>
                libraryIds.Contains(x.PlexLibraryId)
                && (
                    x.Status == LibrarySyncJobStatus.Cancelled
                    || x.Status == LibrarySyncJobStatus.Failed
                    || x.Status == LibrarySyncJobStatus.Completed
                )
            )
            .ResetJobsToQueuedAsync(token);
    }

    public static async Task<int> ResetJobsToQueuedAsync(
        this IQueryable<LibrarySyncJobQueue> queryable,
        CancellationToken token = default
    )
    {
        return await queryable.ExecuteUpdateAsync(
            x =>
                x.SetProperty(y => y.Status, LibrarySyncJobStatus.Queued)
                    .SetProperty(y => y.CreatedAt, DateTime.UtcNow)
                    .SetProperty(y => y.StartedAt, (DateTime?)null)
                    .SetProperty(y => y.CompletedAt, (DateTime?)null)
                    .SetProperty(y => y.ErrorMessage, (string?)null)
                    .SetProperty(y => y.IsServerOffline, false),
            token
        );
    }
}
