namespace Reaparr.Application;

/// <summary>
/// Recalculates the persisted type for every Plex genre from its current name mapping.
/// </summary>
public record RecalculatePlexGenreTypesCommand : ICommand<Result<PlexGenreTypeRecalculationResult>>;

public record PlexGenreTypeRecalculationResult(int ScannedCount, int UpdatedCount);

public class RecalculatePlexGenreTypesCommandValidator : AbstractValidator<RecalculatePlexGenreTypesCommand>
{
    public RecalculatePlexGenreTypesCommandValidator() => RuleFor(x => x).NotNull();
}

public class RecalculatePlexGenreTypesCommandHandler
    : ICommandHandler<RecalculatePlexGenreTypesCommand, Result<PlexGenreTypeRecalculationResult>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public RecalculatePlexGenreTypesCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<RecalculatePlexGenreTypesCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<PlexGenreTypeRecalculationResult>> ExecuteAsync(
        RecalculatePlexGenreTypesCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await Result.Try(async Task<Result<PlexGenreTypeRecalculationResult>> () =>
        {
            var genres = await _dbContext.PlexGenres.AsTracking().ToListAsync(cancellationToken);
            var updatedCount = 0;

            foreach (var genre in genres)
            {
                var calculatedType = genre.Name.ToPlexGenreType();
                if (genre.Type == calculatedType)
                    continue;

                genre.Type = calculatedType;
                updatedCount++;
            }

            if (updatedCount > 0)
                await _dbContext.SaveChangesAsync(cancellationToken);

            var recalculationResult = new PlexGenreTypeRecalculationResult(genres.Count, updatedCount);
            _log.Here()
                .Information(
                    "Recalculated PlexGenre types: scanned {ScannedCount}, updated {UpdatedCount}",
                    recalculationResult.ScannedCount,
                    recalculationResult.UpdatedCount
                );
            return Result.Ok(recalculationResult);
        });

        result.LogIfFailed();

        return result;
    }
}
