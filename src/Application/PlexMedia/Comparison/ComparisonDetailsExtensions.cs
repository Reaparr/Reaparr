namespace Reaparr.Application;

internal static class ComparisonDetailsExtensions
{
    internal static async Task<PlexMediaComparisonState> GetUncomparedComparisonStateAsync(
        this IReaparrDbContext dbContext,
        IScheduler scheduler,
        int libraryId,
        bool isOwned,
        PlexMediaType mediaType,
        CancellationToken cancellationToken
    )
    {
        if (await dbContext.PlexLibraries.AnyAsync(x => x.Id == libraryId && x.Outdated, cancellationToken))
            return PlexMediaComparisonState.NotCompared;

        var candidates = isOwned ? dbContext.PlexLibraries.WhereIsNotOwned() : dbContext.PlexLibraries.WhereIsOwned();
        var libraryIds = await candidates
            .Where(x => x.Type == mediaType && !x.Outdated)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var keys = libraryIds.Select(x =>
            isOwned
                ? PlexLibraryComparisonJob.GetJobKey(libraryId, x)
                : PlexLibraryComparisonJob.GetJobKey(x, libraryId)
        );
        return await scheduler.HasActiveJobs(keys, cancellationToken)
            ? PlexMediaComparisonState.Pending
            : PlexMediaComparisonState.NotCompared;
    }
}
