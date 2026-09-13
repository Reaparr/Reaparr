namespace Reaparr.Application;

public sealed record RebuildMediaOverviewCommand : ICommand<Result>;

public sealed class RebuildMediaOverviewCommandValidator : AbstractValidator<RebuildMediaOverviewCommand>
{
    public RebuildMediaOverviewCommandValidator() => RuleFor(x => x).NotNull();
}

public sealed class RebuildMediaOverviewCommandHandler : ICommandHandler<RebuildMediaOverviewCommand, Result>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly MediaOverviewRebuildCoordinator _coordinator;

    public RebuildMediaOverviewCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        MediaOverviewRebuildCoordinator coordinator
    )
    {
        _dbContextFactory = dbContextFactory;
        _coordinator = coordinator;
    }

    public async Task<Result> ExecuteAsync(RebuildMediaOverviewCommand command, CancellationToken cancellationToken)
    {
        using var lease = await _coordinator.AcquireRebuildLeaseAsync(cancellationToken);

        var movieResult = await RebuildRootAsync(PlexMediaType.Movie, cancellationToken);
        movieResult.LogIfFailed();

        var tvShowResult = await RebuildRootAsync(PlexMediaType.TvShow, cancellationToken);
        tvShowResult.LogIfFailed();

        return Result.Merge(movieResult, tvShowResult);
    }

    private async Task<Result> RebuildRootAsync(PlexMediaType mediaType, CancellationToken cancellationToken)
    {
        using var context = await _dbContextFactory.CreateAsync();

        switch (mediaType)
        {
            case PlexMediaType.Movie:
            {
                var source = await context
                    .PlexMovies.Select(x => new PlexMovieRankSource(
                        x.Id,
                        x.PlexLibraryId,
                        x.SearchTitle,
                        x.Year,
                        x.AddedAt,
                        x.UpdatedAt,
                        x.Duration,
                        x.MediaSize,
                        x.Quality
                    ))
                    .ToListAsync(cancellationToken);
                return await ReplaceAsync(
                    source.BuildMovieSnapshots(),
                    static dbContext => dbContext.MovieMediaOverviewSnapshots,
                    cancellationToken
                );
            }
            case PlexMediaType.TvShow:
            {
                var source = await context
                    .PlexTvShows.Select(x => new PlexTvShowRankSource(
                        x.Id,
                        x.PlexLibraryId,
                        x.SearchTitle,
                        x.Year,
                        x.AddedAt,
                        x.UpdatedAt,
                        x.Duration,
                        x.MediaSize,
                        x.Quality
                    ))
                    .ToListAsync(cancellationToken);
                return await ReplaceAsync(
                    source.BuildTvShowSnapshots(),
                    static dbContext => dbContext.TvShowMediaOverviewSnapshots,
                    cancellationToken
                );
            }
            default:
                return Result.Fail(
                    "Media type {PlexMediaType} cannot be rebuilt as a root overview snapshot",
                    mediaType
                );
        }
    }

    private async Task<Result> ReplaceAsync<TSnapshot>(
        IReadOnlyCollection<TSnapshot> snapshots,
        Func<IReaparrDbContext, DbSet<TSnapshot>> table,
        CancellationToken cancellationToken
    )
        where TSnapshot : class
    {
        using var context = await _dbContextFactory.CreateAsync();
        var result = await context.ExecuteTransactionAsync(
            async (dbContext, transactionToken) =>
            {
                await table(dbContext).ExecuteDeleteAsync(transactionToken);
                if (snapshots.Count > 0)
                    await dbContext.BulkInsertAsync(snapshots.ToList(), BulkConfigPreset.Default, transactionToken);
            },
            cancellationToken
        );
        result.LogIfFailed();
        return result;
    }
}
