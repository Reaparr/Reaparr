using FlexQuery.NET;
using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;
using static Reaparr.Data.MediaByTypeQuerySupport;

namespace Reaparr.Data;

public sealed class GetMovieMediaByTypeCommandValidator : AbstractValidator<GetMovieMediaByTypeCommand>
{
    public GetMovieMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter).NotNull().SetValidator(new MediaQueryFilterValidator(PlexMediaType.Movie));
    }
}

public sealed class GetMovieMediaByTypeCommandHandler
    : ICommandHandler<GetMovieMediaByTypeCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public GetMovieMediaByTypeCommandHandler(IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMovieMediaByTypeCommand command,
        CancellationToken cancellationToken
    )
    {
        var filter = command.Filter;
        var response = CreateResponse(filter);
        var (options, libraryIds, hasUserFilters) = await MediaQueryPreparation.PrepareAsync(
            _dbContext,
            filter,
            cancellationToken
        );
        if (libraryIds.Count == 0)
            return Result.Ok(response);

        var movieQuery = _dbContext.PlexMovies.ApplyFilter(options);
        if (options.Sort.Any(sort => sort.Field.ToSortType() == SortType.ComparisonState))
        {
            var candidatesResult = await LoadComparisonCandidatesAsync(
                movieQuery.ProjectMoviePage(),
                _commandExecutor,
                filter,
                options,
                cancellationToken
            );
            if (candidatesResult.IsCancelledOrFailed)
                return candidatesResult.ToResult<PagedMediaQueryResult>();

            var candidates = candidatesResult.Value;
            SetComparisonSummary(response, candidates, options.Sort, includeQuality: true);
            var pageCandidates = ApplyDtoPaging(candidates, response.Page, response.PageSize);
            var pageMetadata = await LoadMoviePageMetadataAsync(
                pageCandidates.Select(x => x.Id).ToHashSet(),
                cancellationToken
            );
            SetComparisonPage(response, pageCandidates, pageMetadata, includeQuality: true);
        }
        else
        {
            var sortedMovieQuery = movieQuery.ApplySort(options);
            if (filter.ComparisonState.HasValue)
            {
                var candidatesResult = await LoadComparisonCandidatesAsync(
                    sortedMovieQuery.ProjectMoviePage(),
                    _commandExecutor,
                    filter,
                    options,
                    cancellationToken
                );
                if (candidatesResult.IsCancelledOrFailed)
                    return candidatesResult.ToResult<PagedMediaQueryResult>();

                var candidates = candidatesResult.Value;
                response.TotalCount = candidates.Count;
                response.MediaSize = candidates.Sum(x => x.MediaSize);
                response.TotalMediaSize = response.MediaSize;

                var filteredIds = candidates.Select(x => x.Id).ToHashSet();
                await SetNavigationIndexes(
                    response,
                    sortedMovieQuery
                        .Where(x => filteredIds.Contains(x.Id))
                        .ProjectMovieNavigationIndexRows(),
                    options,
                    cancellationToken
                );

                var pageCandidates = ApplyDtoPaging(candidates, response.Page, response.PageSize);
                var pageMetadata = await LoadMoviePageMetadataAsync(
                    pageCandidates.Select(x => x.Id).ToHashSet(),
                    cancellationToken
                );
                SetComparisonPage(response, pageCandidates, pageMetadata, includeQuality: true);
            }
            else
            {
                response.TotalCount = await sortedMovieQuery.CountAsync(cancellationToken);
                response.MediaSize = await sortedMovieQuery.SumAsync(x => x.MediaSize, cancellationToken);
                response.TotalMediaSize = response.MediaSize;

                await SetNavigationIndexes(
                    response,
                    sortedMovieQuery.ProjectMovieNavigationIndexRows(),
                    options,
                    cancellationToken
                );

                var pageCandidates = await sortedMovieQuery
                    .ApplyPaging(options)
                    .ProjectMoviePage()
                    .ToListAsync(cancellationToken);
                var comparisonResult = await ApplyComparisonStateAsync(
                    _commandExecutor,
                    pageCandidates,
                    filter,
                    cancellationToken
                );
                if (comparisonResult.IsCancelledOrFailed)
                    return comparisonResult.ToResult<PagedMediaQueryResult>();

                var pageMetadata = await LoadMoviePageMetadataAsync(
                    pageCandidates.Select(x => x.Id).ToHashSet(),
                    cancellationToken
                );
                SetComparisonPage(response, pageCandidates, pageMetadata, includeQuality: true);
            }
        }

        await MediaQueryResponse.CompleteAsync(
            _dbContext,
            response,
            filter,
            libraryIds,
            hasUserFilters,
            cancellationToken
        );
        return Result.Ok(response);
    }

    private async Task<(List<int> Roles, List<int> Countries, List<int> Genres)> LoadMoviePageMetadataAsync(
        HashSet<int> pageIds,
        CancellationToken cancellationToken
    )
    {
        var rows = await _dbContext
            .PlexMovies.Where(x => pageIds.Contains(x.Id))
            .ProjectMovieMetadataRows()
            .ToListAsync(cancellationToken);

        return (
            rows.SelectMany(x => x.Roles).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Countries).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Genres).Distinct().Order().ToList()
        );
    }
}
