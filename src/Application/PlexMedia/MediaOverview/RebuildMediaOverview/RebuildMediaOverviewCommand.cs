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

    public RebuildMediaOverviewCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        IMediaOverviewRebuildCoordinator coordinator
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
                return await ReplaceAsync(
                    snapshots.AssignRanks(),
                    static dbContext => dbContext.MediaOverviewMovieSnapshots,
                    cancellationToken
                );
            }
            case PlexMediaType.TvShow:
            {
                var snapshots = await context
                    .PlexTvShows.Select(x => new MediaOverviewTvShowSnapshot
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
                return await ReplaceAsync(
                    snapshots.AssignRanks(),
                    static dbContext => dbContext.MediaOverviewTvShowSnapshots,
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
        IList<TSnapshot> snapshots,
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
                    await dbContext.BulkInsertAsync(snapshots, BulkConfigPreset.Default, transactionToken);
            },
            cancellationToken
        );
        result.LogIfFailed();
        return result;
    }
}
