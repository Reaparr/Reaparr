namespace Reaparr.Application;

public sealed record RebuildMediaOverviewCommand : ICommand<Result>;

public sealed class RebuildMediaOverviewCommandValidator : AbstractValidator<RebuildMediaOverviewCommand>
{
    public RebuildMediaOverviewCommandValidator() => RuleFor(x => x).NotNull();
}

public sealed class RebuildMediaOverviewCommandHandler : ICommandHandler<RebuildMediaOverviewCommand, Result>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IMediaOverviewRebuildCoordinator _coordinator;
    private readonly ILogger _log;

    public RebuildMediaOverviewCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        IMediaOverviewRebuildCoordinator coordinator,
        ILogger log
    )
    {
        _dbContextFactory = dbContextFactory;
        _coordinator = coordinator;
        _log = log.ForContext<RebuildMediaOverviewCommandHandler>();
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
        var totalStopwatch = Stopwatch.StartNew();
        using var context = await _dbContextFactory.CreateAsync();

        switch (mediaType)
        {
            case PlexMediaType.Movie:
            {
                var loadStopwatch = Stopwatch.StartNew();
                var snapshots = await context
                    .PlexMovies.Select(x => new MediaOverviewMovieSnapshot
                    {
                        PlexMovieId = x.Id,
                        PlexLibraryId = x.PlexLibraryId,
                        SearchTitle = x.SearchTitle,
                        Year = x.Year,
                        AddedAt = x.AddedAt,
                        UpdatedAt = x.UpdatedAt,
                        Duration = x.Duration,
                        MediaSize = x.MediaSize,
                        Quality = x.Quality,
                        TitleRank = 0,
                        YearRank = 0,
                        AddedAtRank = 0,
                        UpdatedAtRank = 0,
                        DurationRank = 0,
                        MediaSizeRank = 0,
                        QualityRank = 0,
                    })
                    .ToListAsync(cancellationToken);
                LogPhase(mediaType, "Load", snapshots.Count, loadStopwatch.Elapsed);

                var rankStopwatch = Stopwatch.StartNew();
                snapshots.AssignRanks();
                LogPhase(mediaType, "Rank", snapshots.Count, rankStopwatch.Elapsed);

                var replaceResult = await ReplaceAsync(
                    mediaType,
                    snapshots,
                    static dbContext => dbContext.MediaOverviewMovieSnapshots,
                    cancellationToken
                );
                LogPhase(mediaType, "Total", snapshots.Count, totalStopwatch.Elapsed);
                return replaceResult;
            }
            case PlexMediaType.TvShow:
            {
                var loadStopwatch = Stopwatch.StartNew();
                var snapshots = await context
                    .PlexTvShows.AsNoTracking()
                    .Select(x => new MediaOverviewTvShowSnapshot
                    {
                        PlexTvShowId = x.Id,
                        PlexLibraryId = x.PlexLibraryId,
                        SearchTitle = x.SearchTitle,
                        Year = x.Year,
                        AddedAt = x.AddedAt,
                        UpdatedAt = x.UpdatedAt,
                        Duration = x.Duration,
                        MediaSize = x.MediaSize,
                        Quality = x.Quality,
                        TitleRank = 0,
                        YearRank = 0,
                        AddedAtRank = 0,
                        UpdatedAtRank = 0,
                        DurationRank = 0,
                        MediaSizeRank = 0,
                        QualityRank = 0,
                    })
                    .ToListAsync(cancellationToken);
                LogPhase(mediaType, "Load", snapshots.Count, loadStopwatch.Elapsed);

                var rankStopwatch = Stopwatch.StartNew();
                snapshots.AssignRanks();
                LogPhase(mediaType, "Rank", snapshots.Count, rankStopwatch.Elapsed);

                var replaceResult = await ReplaceAsync(
                    mediaType,
                    snapshots,
                    static dbContext => dbContext.MediaOverviewTvShowSnapshots,
                    cancellationToken
                );
                LogPhase(mediaType, "Total", snapshots.Count, totalStopwatch.Elapsed);
                return replaceResult;
            }
            default:
                return Result.Fail(
                    "Media type {PlexMediaType} cannot be rebuilt as a root overview snapshot",
                    mediaType
                );
        }
    }

    private async Task<Result> ReplaceAsync<TSnapshot>(
        PlexMediaType mediaType,
        IList<TSnapshot> snapshots,
        Func<IReaparrDbContext, DbSet<TSnapshot>> table,
        CancellationToken cancellationToken
    )
        where TSnapshot : class
    {
        var stopwatch = Stopwatch.StartNew();
        using var context = await _dbContextFactory.CreateAsync();
        var result = await context.ExecuteTransactionAsync(
            async (dbContext, transactionToken) =>
            {
                await table(dbContext).ExecuteDeleteAsync(transactionToken);
                if (snapshots.Count > 0)
                    await dbContext.BulkInsertAsync(snapshots, BulkConfigPreset.Default, transactionToken);
            },
            cancellationToken
        );
        LogPhase(mediaType, "Replace", snapshots.Count, stopwatch.Elapsed);
        result.LogIfFailed();
        return result;
    }

    private void LogPhase(PlexMediaType mediaType, string phase, int rowCount, TimeSpan elapsed) =>
        _log.Here()
            .Debug(
                "Media overview rebuild phase {Phase} completed for {MediaType}: {RowCount} rows in {ElapsedMilliseconds} ms",
                phase,
                mediaType,
                rowCount,
                elapsed.TotalMilliseconds
            );
}
