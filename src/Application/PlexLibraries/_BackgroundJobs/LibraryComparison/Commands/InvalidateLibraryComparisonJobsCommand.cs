namespace Reaparr.Application;

/// <summary>
/// Invalidates every comparison in which one of the supplied libraries participates.
/// Queued jobs and persisted comparison results are deleted.
/// </summary>
public record InvalidateLibraryComparisonJobsCommand(IReadOnlyCollection<int> PlexLibraryIds) : ICommand<Result>;

public class InvalidateLibraryComparisonJobsCommandValidator : AbstractValidator<InvalidateLibraryComparisonJobsCommand>
{
    public InvalidateLibraryComparisonJobsCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.PlexLibraryIds).NotEmpty();
        RuleForEach(x => x.PlexLibraryIds).GreaterThan(0);
    }
}

public class InvalidateLibraryComparisonJobsCommandHandler
    : ICommandHandler<InvalidateLibraryComparisonJobsCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IScheduler _scheduler;

    public InvalidateLibraryComparisonJobsCommandHandler(ILogger log, IReaparrDbContext dbContext, IScheduler scheduler)
    {
        _log = log.ForContext<InvalidateLibraryComparisonJobsCommandHandler>();
        _dbContext = dbContext;
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(
        InvalidateLibraryComparisonJobsCommand command,
        CancellationToken cancellationToken
    )
    {
        var affectedLibraryIds = command.PlexLibraryIds.ToHashSet();
        var comparisonJobKeys = await _scheduler.GetJobKeys(JobTypes.LibraryComparisonJob, cancellationToken);
        var comparisonJobs = await Task.WhenAll(
            comparisonJobKeys.Select(async jobKey =>
                (JobKey: jobKey, Detail: await _scheduler.GetJobDetail(jobKey, cancellationToken))
            )
        );

        var jobKeysToDelete = comparisonJobs
            .Where(x =>
            {
                var payload = x.Detail?.JobDataMap.GetPayload<PlexLibraryComparisonJobPayload>();
                return payload is not null
                    && (
                        affectedLibraryIds.Contains(payload.OwnedPlexLibraryId)
                        || affectedLibraryIds.Contains(payload.RemotePlexLibraryId)
                    );
            })
            .Select(x => x.JobKey)
            .ToList();

        var result = await _scheduler.DeleteBatchJobs(jobKeysToDelete, cancellationToken);
        if (result.IsFailed)
            return result;

        var comparisonResult = await _dbContext.ExecuteTransactionAsync(async (ctx, ct) =>
        {
            await ctx.PlexMovieComparisons
                .Where(x => affectedLibraryIds.Contains(x.OwnedPlexLibraryId) || affectedLibraryIds.Contains(x.RemotePlexLibraryId))
                .ExecuteDeleteAsync(ct);
            await ctx.PlexTvShowComparisons
                .Where(x => affectedLibraryIds.Contains(x.OwnedPlexLibraryId) || affectedLibraryIds.Contains(x.RemotePlexLibraryId))
                .ExecuteDeleteAsync(ct);
            await ctx.PlexSeasonComparisons
                .Where(x => affectedLibraryIds.Contains(x.OwnedPlexLibraryId) || affectedLibraryIds.Contains(x.RemotePlexLibraryId))
                .ExecuteDeleteAsync(ct);
            await ctx.PlexEpisodeComparisons
                .Where(x => affectedLibraryIds.Contains(x.OwnedPlexLibraryId) || affectedLibraryIds.Contains(x.RemotePlexLibraryId))
                .ExecuteDeleteAsync(ct);
            await ctx.PlexMusicArtistComparisons
                .Where(x => affectedLibraryIds.Contains(x.OwnedPlexLibraryId) || affectedLibraryIds.Contains(x.RemotePlexLibraryId))
                .ExecuteDeleteAsync(ct);
            await ctx.PlexMusicAlbumComparisons
                .Where(x => affectedLibraryIds.Contains(x.OwnedPlexLibraryId) || affectedLibraryIds.Contains(x.RemotePlexLibraryId))
                .ExecuteDeleteAsync(ct);
            await ctx.PlexMusicTrackComparisons
                .Where(x => affectedLibraryIds.Contains(x.OwnedPlexLibraryId) || affectedLibraryIds.Contains(x.RemotePlexLibraryId))
                .ExecuteDeleteAsync(ct);
            await ctx.PlexComparisonScopes
                .Where(x => affectedLibraryIds.Contains(x.OwnedPlexLibraryId) || affectedLibraryIds.Contains(x.RemotePlexLibraryId))
                .ExecuteDeleteAsync(ct);
        }, cancellationToken);
        if (comparisonResult.IsFailed)
            return comparisonResult;

        _log.Here().Debug("Invalidated comparison jobs for libraries {LibraryIds}", command.PlexLibraryIds);

        return result;
    }
}
