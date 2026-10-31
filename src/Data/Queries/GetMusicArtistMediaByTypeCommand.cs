using FlexQuery.NET;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;
using static Reaparr.Data.MediaByTypeQuerySupport;

namespace Reaparr.Data;

public sealed class GetMusicArtistMediaByTypeCommandValidator : AbstractValidator<GetMusicArtistMediaByTypeCommand>
{
    public GetMusicArtistMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter)
            .NotNull()
            .ChildRules(filter =>
            {
                filter.RuleFor(x => x.MediaType).Equal(PlexMediaType.MusicArtist);
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

public sealed class GetMusicArtistMediaByTypeCommandHandler
    : ICommandHandler<GetMusicArtistMediaByTypeCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public GetMusicArtistMediaByTypeCommandHandler(IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMusicArtistMediaByTypeCommand command,
        CancellationToken ct
    )
    {
        var filter = command.Filter;
        var response = CreateResponse(filter);
        var preparation = await MediaQueryPreparation.PrepareAsync(_dbContext, filter, ct);
        if (preparation.LibraryIds.Count == 0)
            return Result.Ok(response);

        var options = preparation.Options;
        if (options.Sort.Count == 0 && filter.PlexLibraryId > 0)
            options.Sort.Add(new SortNode { Field = SortType.Title.ToSortValue(), Descending = false });

        if (options.Sort.Any(sort => sort.Field.ToSortType() == SortType.ComparisonState))
        {
            var candidateResult = await LoadComparisonCandidatesAsync(
                _dbContext.PlexMusicArtists.ApplyFilter(options).ProjectMusicArtistComparisonCandidates(),
                _commandExecutor,
                filter,
                options,
                ct
            );
            if (candidateResult.IsCancelledOrFailed)
                return candidateResult.ToResult<PagedMediaQueryResult>();

            var candidates = candidateResult.Value;
            SetComparisonSummary(response, candidates, options.Sort, includeQuality: false);
            var pageCandidates =
                response.PageSize == 0
                    ? candidates
                    : candidates.Skip((response.Page - 1) * response.PageSize).Take(response.PageSize).ToList();
            var metadata = await LoadMusicArtistPageMetadataAsync(pageCandidates.Select(x => x.Id), ct);
            SetComparisonPage(response, pageCandidates, metadata, includeQuality: false);
            await MediaQueryResponse.CompleteAsync(
                _dbContext,
                response,
                filter,
                preparation.LibraryIds,
                preparation.HasUserFilters,
                ct
            );
            return Result.Ok(response);
        }

        if (filter.ComparisonState.HasValue)
        {
            var comparisonQuery = _dbContext
                .PlexMusicArtists.ApplyFilter(options)
                .ApplyMusicArtistSortIfRequired(options, filter.PlexLibraryId == 0);
            var candidateResult = await LoadComparisonCandidatesAsync(
                comparisonQuery.ProjectMusicArtistPage(),
                _commandExecutor,
                filter,
                options,
                ct
            );
            if (candidateResult.IsCancelledOrFailed)
                return candidateResult.ToResult<PagedMediaQueryResult>();

            var candidates = candidateResult.Value;
            SetComparisonSummary(response, candidates, options.Sort, includeQuality: false);
            var pageCandidates =
                response.PageSize == 0
                    ? candidates
                    : candidates.Skip((response.Page - 1) * response.PageSize).Take(response.PageSize).ToList();
            var metadata = await LoadMusicArtistPageMetadataAsync(pageCandidates.Select(x => x.Id), ct);
            SetComparisonPage(response, pageCandidates, metadata, includeQuality: false);
            await MediaQueryResponse.CompleteAsync(
                _dbContext,
                response,
                filter,
                preparation.LibraryIds,
                preparation.HasUserFilters,
                ct
            );
            return Result.Ok(response);
        }

        var artistQuery = _dbContext
            .PlexMusicArtists.ApplyFilter(options)
            .ApplyMusicArtistSortIfRequired(options, filter.PlexLibraryId == 0);
        response.TotalCount = await artistQuery.CountAsync(ct);
        response.MediaSize = await artistQuery.SumAsync(x => x.MediaSize, ct);
        response.TotalMediaSize = response.MediaSize;
        var materialized = await artistQuery.ProjectMusicArtistNavigationIndexRows().ToListAsync(ct);
        response.NavigationIndexes = MediaNavigationIndexBuilder.Build(
            materialized,
            ((IReadOnlyList<SortNode>)options.Sort).Count == 0
                ? SortType.Title
                : ((IReadOnlyList<SortNode>)options.Sort)[0].Field.ToSortType()
        );

        response.Items = await artistQuery.ApplyPaging(options).ProjectMusicArtistPage().ToListAsync(ct);
        Result ret;
        if (response.Items.Count == 0)
            ret = Result.Ok();
        else
        {
            ret = await _commandExecutor.Send(
                new ApplyComparisonStateCommand(
                    response.Items,
                    filter.MediaType,
                    filter.PlexLibraryId > 0 ? filter.PlexLibraryId : null
                ),
                ct
            );
        }

        var comparisonResult = ret;
        if (comparisonResult.IsCancelledOrFailed)
            return comparisonResult.ToResult<PagedMediaQueryResult>();

        var pageMetadata = await LoadMusicArtistPageMetadataAsync(response.Items.Select(x => x.Id), ct);
        SetComparisonPage(response, response.Items, pageMetadata, includeQuality: false);
        await MediaQueryResponse.CompleteAsync(
            _dbContext,
            response,
            filter,
            preparation.LibraryIds,
            preparation.HasUserFilters,
            ct
        );
        return Result.Ok(response);
    }

    private async Task<(List<int> Roles, List<int> Countries, List<int> Genres)> LoadMusicArtistPageMetadataAsync(
        IEnumerable<int> pageIds,
        CancellationToken ct
    )
    {
        var ids = pageIds.ToHashSet();
        var rows = await _dbContext
            .PlexMusicArtists.Where(x => ids.Contains(x.Id))
            .ProjectMusicArtistMetadataRows()
            .ToListAsync(ct);

        return (
            rows.SelectMany(x => x.Roles).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Countries).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Genres).Distinct().Order().ToList()
        );
    }
}
