namespace Reaparr.Application;

public class ApplyComparisonStateCommandValidator : AbstractValidator<ApplyComparisonStateCommand>
{
    public ApplyComparisonStateCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Items).NotNull().WithMessage("Items must not be null.");
        RuleFor(x => x.PlexLibraryId)
            .Must(x => !x.HasValue || x.Value > 0)
            .WithMessage("PlexLibraryId must be greater than 0 when specified.");
        RuleForEach(x => x.Items)
            .Must(x => x.PlexLibraryId > 0)
            .When(x => x.PlexLibraryId is null)
            .WithMessage("Items must contain a library ID when PlexLibraryId is null.");
        RuleFor(x => x.MediaType)
            .Must(x => x is PlexMediaType.Movie or PlexMediaType.TvShow)
            .WithMessage("MediaType must be Movie or TvShow.");
    }
}

/// <summary>
/// Dispatches comparison state projection to type- and ownership-specific sub-handlers
/// via <see cref="ICommandExecutor"/>. Resolves library scope from the explicit target
/// or the item library IDs, then sends the appropriate sub-command.
/// </summary>
public class ApplyComparisonStateCommandHandler : ICommandHandler<ApplyComparisonStateCommand, Result>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public ApplyComparisonStateCommandHandler(IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result> ExecuteAsync(ApplyComparisonStateCommand command, CancellationToken ct)
    {
        if (command.Items.Count == 0)
            return Result.Ok();

        var itemsByLibrary = command.PlexLibraryId is int plexLibraryId
            ? new Dictionary<int, List<PlexMediaSlimDTO>> { [plexLibraryId] = command.Items }
            : GroupItemsByLibrary(command.Items);
        var libraryIds = itemsByLibrary.Keys.ToArray();
        var ownedLibraryIds = await _dbContext
            .PlexLibraries.WhereIsOwned()
            .Where(x => libraryIds.AsEnumerable().Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync(ct);

        foreach (var (libraryId, libraryItems) in itemsByLibrary)
        {
            var result = await ApplyForLibraryAsync(
                libraryItems,
                libraryId,
                command.MediaType,
                ownedLibraryIds.Contains(libraryId),
                ct
            );
            if (result.IsFailed)
                return result;
        }

        return Result.Ok();
    }

    private static Dictionary<int, List<PlexMediaSlimDTO>> GroupItemsByLibrary(List<PlexMediaSlimDTO> items)
    {
        var itemsByLibrary = new Dictionary<int, List<PlexMediaSlimDTO>>();
        foreach (var item in items)
        {
            if (!itemsByLibrary.TryGetValue(item.PlexLibraryId, out var libraryItems))
            {
                libraryItems = [];
                itemsByLibrary.Add(item.PlexLibraryId, libraryItems);
            }

            libraryItems.Add(item);
        }

        return itemsByLibrary;
    }

    private async Task<Result> ApplyForLibraryAsync(
        List<PlexMediaSlimDTO> items,
        int plexLibraryId,
        PlexMediaType mediaType,
        bool isOwned,
        CancellationToken ct
    )
    {
        if (isOwned)
        {
            switch (mediaType)
            {
                case PlexMediaType.Movie:
                    return await _commandExecutor.Send(
                        new ApplyOwnedMovieComparisonStateCommand(items, plexLibraryId),
                        ct
                    );
                case PlexMediaType.TvShow:
                    return await _commandExecutor.Send(
                        new ApplyOwnedTvShowComparisonStateCommand(items, plexLibraryId),
                        ct
                    );
                default:
                    return Result.Fail(
                        "Unsupported media type {PlexMediaType} for owned library {PlexLibraryId}",
                        mediaType,
                        plexLibraryId
                    );
            }
        }

        switch (mediaType)
        {
            case PlexMediaType.Movie:
                return await _commandExecutor.Send(
                    new ApplyRemoteMovieComparisonStateCommand(items, plexLibraryId),
                    ct
                );
            case PlexMediaType.TvShow:
                return await _commandExecutor.Send(
                    new ApplyRemoteTvShowComparisonStateCommand(items, plexLibraryId),
                    ct
                );
            default:
                return Result.Fail(
                    "Unsupported media type {PlexMediaType} for remote library {PlexLibraryId}",
                    mediaType,
                    plexLibraryId
                );
        }
    }
}
