namespace Reaparr.Application;

public record GetMovieMediaComparisonDetailsCommand(int PlexMediaId) : ICommand<Result<PlexMediaComparisonDetailsDTO>>;

public class GetMovieMediaComparisonDetailsCommandValidator : AbstractValidator<GetMovieMediaComparisonDetailsCommand>
{
    public GetMovieMediaComparisonDetailsCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.PlexMediaId).GreaterThan(0);
    }
}

public class GetMovieMediaComparisonDetailsCommandHandler
    : ICommandHandler<GetMovieMediaComparisonDetailsCommand, Result<PlexMediaComparisonDetailsDTO>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly IScheduler _scheduler;
    private int _rowId;

    public GetMovieMediaComparisonDetailsCommandHandler(IReaparrDbContext dbContext, IScheduler scheduler)
    {
        _dbContext = dbContext;
        _scheduler = scheduler;
    }

    public async Task<Result<PlexMediaComparisonDetailsDTO>> ExecuteAsync(
        GetMovieMediaComparisonDetailsCommand command,
        CancellationToken ct
    )
    {
        var movie = await _dbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .SingleOrDefaultAsync(x => x.Id == command.PlexMediaId, ct);
        if (movie is null)
            return ResultExtensions.EntityNotFound(nameof(PlexMovie), command.PlexMediaId).LogError();

        var isOwned = await _dbContext.PlexLibraries.WhereIsOwned().AnyAsync(x => x.Id == movie.PlexLibraryId, ct);
        var currentLibraryIds = isOwned
            ? await _dbContext.GetCurrentRemoteLibraryIds(movie.PlexLibraryId, PlexMediaType.Movie, ct)
            : await _dbContext.GetCurrentOwnedLibraryIds(movie.PlexLibraryId, PlexMediaType.Movie, ct);
        var rows = new List<ComparisonDetailsRow>();
        PlexMediaComparisonState state;
        if (currentLibraryIds.Count == 0)
            state = await _dbContext.GetUncomparedComparisonStateAsync(
                _scheduler,
                movie.PlexLibraryId,
                isOwned,
                PlexMediaType.Movie,
                ct
            );
        else
        {
            rows = isOwned
                ? await GetOwnedMovieRowsAsync(movie, currentLibraryIds, ct)
                : await GetRemoteMovieRowsAsync(movie, currentLibraryIds, ct);
            state = PlexMediaComparisonDetailsMapper.ToParentState(rows);
        }

        return Result.Ok(
            new PlexMediaComparisonDetailsDTO
            {
                PlexMediaId = movie.Id,
                Type = PlexMediaType.Movie,
                State = state,
                Rows = PlexMediaComparisonDetailsMapper.ToDtoRows(rows),
            }
        );
    }

    private async Task<List<ComparisonDetailsRow>> GetRemoteMovieRowsAsync(
        PlexMovie movie,
        HashSet<int> currentOwnedLibraryIds,
        CancellationToken ct
    )
    {
        var hits = await _dbContext
            .PlexMovieComparisons.Where(x =>
                x.RemotePlexLibraryId == movie.PlexLibraryId
                && currentOwnedLibraryIds.Contains(x.OwnedPlexLibraryId)
                && x.RemotePlexMediaId == movie.Id
            )
            .ToListAsync(ct);

        if (hits.Count == 0)
            return
            [
                PlexMediaComparisonDetailsMapper.ToComparisonDetailsRow(
                    rowId: ++_rowId,
                    parentRowId: null,
                    level: 0,
                    plexMediaId: movie.Id,
                    type: PlexMediaType.Movie,
                    title: movie.Title,
                    state: PlexMediaComparisonState.Missing,
                    remoteQuality: movie.VideoQuality,
                    ownedQuality: VideoQuality.None,
                    remoteLocation: FirstFileNameOrEmpty(movie),
                    ownedLocation: string.Empty,
                    remotePlexLibraryId: movie.PlexLibraryId,
                    remotePlexServerId: await _dbContext.GetPlexServerIdFromPlexLibraryId(movie.PlexLibraryId)
                ),
            ];

        return await CreateRemoteMovieUpgradeRowsAsync(movie, hits, ct);
    }

    private async Task<List<ComparisonDetailsRow>> CreateRemoteMovieUpgradeRowsAsync(
        PlexMovie movie,
        List<PlexMovieComparison> hits,
        CancellationToken ct
    )
    {
        var upgradeHits = hits.Where(x => x.HitState == PlexMediaComparisonHitState.HigherQuality).ToList();
        if (upgradeHits.Count == 0)
            return [];

        var ownedMovieIds = upgradeHits.Select(x => x.OwnedPlexMediaId).ToHashSet();
        var ownedMovies = await _dbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .Where(x => ownedMovieIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var remoteServerIds = await GetPlexServerIdsByLibraryIdAsync(
            upgradeHits.Select(x => x.RemotePlexLibraryId),
            ct
        );

        return upgradeHits
            .Select(hit =>
            {
                ownedMovies.TryGetValue(hit.OwnedPlexMediaId, out var ownedMovie);
                return PlexMediaComparisonDetailsMapper.ToComparisonDetailsRow(
                    rowId: ++_rowId,
                    parentRowId: null,
                    level: 0,
                    plexMediaId: movie.Id,
                    type: PlexMediaType.Movie,
                    title: movie.Title,
                    state: PlexMediaComparisonState.HigherQuality,
                    remoteQuality: hit.RemoteQuality,
                    ownedQuality: hit.OwnedQuality,
                    remoteLocation: FirstFileNameOrEmpty(movie),
                    ownedLocation: FirstFileNameOrEmpty(ownedMovie),
                    remotePlexLibraryId: hit.RemotePlexLibraryId,
                    remotePlexServerId: remoteServerIds.GetValueOrDefault(hit.RemotePlexLibraryId)
                );
            })
            .ToList();
    }

    private async Task<List<ComparisonDetailsRow>> GetOwnedMovieRowsAsync(
        PlexMovie movie,
        HashSet<int> currentRemoteLibraryIds,
        CancellationToken ct
    )
    {
        var upgradeHits = await _dbContext
            .PlexMovieComparisons.Where(x =>
                currentRemoteLibraryIds.Contains(x.RemotePlexLibraryId)
                && x.OwnedPlexLibraryId == movie.PlexLibraryId
                && x.OwnedPlexMediaId == movie.Id
                && x.HitState == PlexMediaComparisonHitState.HigherQuality
            )
            .ToListAsync(ct);

        if (upgradeHits.Count == 0)
            return [];

        var remoteMovieIds = upgradeHits.Select(x => x.RemotePlexMediaId).ToHashSet();
        var remoteMovies = await _dbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .Where(x => remoteMovieIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var remoteServerIds = await GetPlexServerIdsByLibraryIdAsync(
            upgradeHits.Select(x => x.RemotePlexLibraryId),
            ct
        );

        return upgradeHits
            .Select(hit =>
            {
                remoteMovies.TryGetValue(hit.RemotePlexMediaId, out var remoteMovie);
                return PlexMediaComparisonDetailsMapper.ToComparisonDetailsRow(
                    rowId: ++_rowId,
                    parentRowId: null,
                    level: 0,
                    plexMediaId: remoteMovie?.Id ?? hit.RemotePlexMediaId,
                    type: PlexMediaType.Movie,
                    title: remoteMovie?.Title ?? movie.Title,
                    state: PlexMediaComparisonState.HigherQuality,
                    remoteQuality: hit.RemoteQuality,
                    ownedQuality: hit.OwnedQuality,
                    remoteLocation: FirstFileNameOrEmpty(remoteMovie),
                    ownedLocation: FirstFileNameOrEmpty(movie),
                    remotePlexLibraryId: hit.RemotePlexLibraryId,
                    remotePlexServerId: remoteServerIds.GetValueOrDefault(hit.RemotePlexLibraryId)
                );
            })
            .ToList();
    }

    private static string FirstFileNameOrEmpty(PlexMovie? movie) =>
        movie?.MediaDataList.Select(x => x.GetFileName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
        ?? string.Empty;

    private async Task<Dictionary<int, int>> GetPlexServerIdsByLibraryIdAsync(
        IEnumerable<int> plexLibraryIds,
        CancellationToken ct
    )
    {
        var ids = plexLibraryIds.ToHashSet();
        return await _dbContext
            .PlexLibraries.Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.PlexServerId, ct);
    }
}
