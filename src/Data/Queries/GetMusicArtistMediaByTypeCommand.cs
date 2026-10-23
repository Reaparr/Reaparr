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
            .SetValidator(new MediaQueryFilterValidator(PlexMediaType.MusicArtist));
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
        options.Sort.RemoveAll(static sort =>
        {
            var field = sort.Field.ToSortType();
            return field is SortType.Year or SortType.VideoQuality;
        });
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
            var pageCandidates = ApplyDtoPaging(candidates, response.Page, response.PageSize);
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
            var candidateResult = await LoadComparisonCandidatesAsync(
                _dbContext
                    .PlexMusicArtists.ApplyFilter(options)
                    .ApplySort(options)
                    .ProjectMusicArtistPage(),
                _commandExecutor,
                filter,
                options,
                ct
            );
            if (candidateResult.IsCancelledOrFailed)
                return candidateResult.ToResult<PagedMediaQueryResult>();

            var candidates = candidateResult.Value;
            SetComparisonSummary(response, candidates, options.Sort, includeQuality: false);
            var pageCandidates = ApplyDtoPaging(candidates, response.Page, response.PageSize);
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

        var artistQuery = _dbContext.PlexMusicArtists.ApplyFilter(options).ApplySort(options);
        response.TotalCount = await artistQuery.CountAsync(ct);
        response.MediaSize = await artistQuery.SumAsync(x => x.MediaSize, ct);
        response.TotalMediaSize = response.MediaSize;
        await SetNavigationIndexes(
            response,
            artistQuery.ProjectMusicArtistNavigationIndexRows(),
            options,
            ct
        );

        response.Items = await artistQuery
            .ApplyPaging(options)
            .ProjectMusicArtistPage()
            .ToListAsync(ct);
        var comparisonResult = await ApplyComparisonStateAsync(_commandExecutor, response.Items, filter, ct);
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
