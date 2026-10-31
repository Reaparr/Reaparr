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
        RuleFor(x => x.Filter)
            .NotNull()
            .ChildRules(filter =>
            {
                filter.RuleFor(x => x.MediaType).Equal(PlexMediaType.Movie);
                filter.RuleFor(x => x.ComparisonState).Null().When(x => !x.MediaType.SupportsComparison());
                filter
                    .RuleFor(x => x.ComparisonState)
                    .Must(x =>
                        x
                            is null
                                or PlexMediaComparisonState.NotCompared
                                or PlexMediaComparisonState.Pending
                                or PlexMediaComparisonState.Owned
                                or PlexMediaComparisonState.Missing
                                or PlexMediaComparisonState.Partial
                    )
                    .When(x => x.MediaType == PlexMediaType.MusicArtist);
                filter
                    .RuleFor(x => x.Parameters.Sort)
                    .Must((value, sort) => sort.IsSupportedSort(value.MediaType))
                    .WithMessage("Sort contains an unsupported field.")
                    .When(x => x.Parameters is not null);
            });
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
            var pageCandidates =
                response.PageSize == 0
                    ? candidates
                    : candidates.Skip((response.Page - 1) * response.PageSize).Take(response.PageSize).ToList();
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
                var materialized = await sortedMovieQuery
                    .Where(x => filteredIds.Contains(x.Id))
                    .ProjectMovieNavigationIndexRows()
                    .ToListAsync(cancellationToken);
                response.NavigationIndexes = MediaNavigationIndexBuilder.Build(
                    materialized,
                    ((IReadOnlyList<SortNode>)options.Sort).Count == 0
                        ? SortType.Title
                        : ((IReadOnlyList<SortNode>)options.Sort)[0].Field.ToSortType()
                );

                var pageCandidates =
                    response.PageSize == 0
                        ? candidates
                        : candidates.Skip((response.Page - 1) * response.PageSize).Take(response.PageSize).ToList();
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

                var materialized = await sortedMovieQuery
                    .ProjectMovieNavigationIndexRows()
                    .ToListAsync(cancellationToken);
                response.NavigationIndexes = MediaNavigationIndexBuilder.Build(
                    materialized,
                    ((IReadOnlyList<SortNode>)options.Sort).Count == 0
                        ? SortType.Title
                        : ((IReadOnlyList<SortNode>)options.Sort)[0].Field.ToSortType()
                );

                var pageCandidates = await sortedMovieQuery
                    .ApplyPaging(options)
                    .ProjectMoviePage()
                    .ToListAsync(cancellationToken);
                Result ret;
                if (pageCandidates.Count == 0)
                    ret = Result.Ok();
                else
                {
                    ret = await _commandExecutor.Send(
                        new ApplyComparisonStateCommand(
                            pageCandidates,
                            filter.MediaType,
                            filter.PlexLibraryId > 0 ? filter.PlexLibraryId : null
                        ),
                        cancellationToken
                    );
                }

                var comparisonResult = ret;
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
