using FlexQuery.NET;
using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;
using static Reaparr.Data.MediaByTypeQuerySupport;

namespace Reaparr.Data;

public record GetTvShowMediaByTypeCommand(MediaQueryFilter Filter, QueryOptions Options)
    : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetTvShowMediaByTypeCommandValidator : AbstractValidator<GetTvShowMediaByTypeCommand>
{
    public GetTvShowMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.TvShow).When(x => x.Filter is not null);
    }
}

public sealed class GetTvShowMediaByTypeCommandHandler
    : ICommandHandler<GetTvShowMediaByTypeCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public GetTvShowMediaByTypeCommandHandler(IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetTvShowMediaByTypeCommand command,
        CancellationToken cancellationToken
    )
    {
        var filter = command.Filter;
        var options = command.Options;
        var response = CreateResponse(filter);

        if (options.Sort.Any(sort => sort.Field.ToSortType() == SortType.ComparisonState))
        {
            var candidatesResult = await LoadComparisonCandidatesAsync(
                ProjectTvShowComparisonCandidates(options),
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
            var pageMetadata = await LoadTvShowPageMetadataAsync(
                pageCandidates.Select(x => x.Id).ToHashSet(),
                cancellationToken
            );
            SetComparisonPage(response, pageCandidates, pageMetadata, includeQuality: true);
            return Result.Ok(response);
        }
        var tvShowQuery = _dbContext
            .PlexTvShows.Include(x => x.Qualities)
            .Include(x => x.Actors)
            .Include(x => x.Countries)
            .Include(x => x.Genres)
            .ApplyFilter(options)
            .ApplySort(options);

        if (filter.ComparisonState.HasValue)
        {
            var tvShows = await tvShowQuery.ToListAsync(cancellationToken);
            var tvShowDtos = tvShows.Select(x => x.ToSlimDTOMapper()).ToList();
            var comparisonResult = await ApplyComparisonStateAsync(
                _commandExecutor,
                tvShowDtos,
                filter,
                cancellationToken
            );
            if (comparisonResult.IsCancelledOrFailed)
                return comparisonResult.ToResult<PagedMediaQueryResult>();

            var filteredDtos = tvShowDtos
                .Where(x => x.ComparisonId == filter.ComparisonState.Value.ToComparisonId())
                .ToList();
            var filteredIds = filteredDtos.Select(x => x.Id).ToHashSet();
            var filteredTvShows = tvShows.Where(x => filteredIds.Contains(x.Id)).ToList();

            response.NavigationIndexes = MediaNavigationIndexBuilder.Build(
                filteredTvShows.Select(x => new MediaNavigationIndexRow
                {
                    SearchTitle = x.SearchTitle,
                    Year = x.Year,
                    QualityValue = (int?)x.VideoQuality,
                    Duration = x.Duration,
                    AddedAt = x.AddedAt,
                    UpdatedAt = x.UpdatedAt,
                    MediaSize = x.MediaSize,
                }),
                ((IReadOnlyList<SortNode>)options.Sort).Count == 0
                    ? SortType.Title
                    : ((IReadOnlyList<SortNode>)options.Sort)[0].Field.ToSortType()
            );

            response.TotalCount = filteredDtos.Count;
            response.MediaSize = filteredDtos.Sum(x => x.MediaSize);
            response.TotalMediaSize = response.MediaSize;

            var pagedDtos = ApplyDtoPaging(filteredDtos, response.Page, response.PageSize);
            var pagedIds = pagedDtos.Select(x => x.Id).ToHashSet();
            var pagedTvShows = filteredTvShows.Where(x => pagedIds.Contains(x.Id)).ToList();
            SetComparisonPage(response, pagedDtos, GetTvShowMetadata(pagedTvShows), includeQuality: true);
        }
        else
        {
            response.TotalCount = await tvShowQuery.CountAsync(cancellationToken);
            response.MediaSize = await tvShowQuery.SumAsync(x => x.MediaSize, cancellationToken);
            response.TotalMediaSize = response.MediaSize;

            await SetNavigationIndexes(
                response,
                tvShowQuery.Select(x => new MediaNavigationIndexRow
                {
                    SearchTitle = x.SearchTitle,
                    Year = x.Year,
                    QualityValue = (int?)x.VideoQuality,
                    Duration = x.Duration,
                    AddedAt = x.AddedAt,
                    UpdatedAt = x.UpdatedAt,
                    MediaSize = x.MediaSize,
                }),
                options,
                cancellationToken
            );

            var tvShows = await tvShowQuery.ApplyPaging(options).ToListAsync(cancellationToken);
            var tvShowDtos = tvShows.Select(x => x.ToSlimDTOMapper()).ToList();
            var comparisonResult = await ApplyComparisonStateAsync(
                _commandExecutor,
                tvShowDtos,
                filter,
                cancellationToken
            );
            if (comparisonResult.IsCancelledOrFailed)
                return comparisonResult.ToResult<PagedMediaQueryResult>();

            SetComparisonPage(response, tvShowDtos, GetTvShowMetadata(tvShows), includeQuality: true);
        }

        return Result.Ok(response);
    }

    private IQueryable<PlexMediaSlimDTO> ProjectTvShowComparisonCandidates(QueryOptions options) =>
        _dbContext
            .PlexTvShows.ApplyFilter(options)
            .Select(x => new PlexMediaSlimDTO
            {
                Id = x.Id,
                PlexApiRatingKey = x.PlexApiRatingKey,
                PlexApiMetaDataKey = x.PlexApiMetaDataKey,
                Title = x.Title,
                SearchTitle = x.SearchTitle,
                SortIndex = x.SortIndex,
                Year = x.Year,
                Duration = x.Duration,
                MediaSize = x.MediaSize,
                ChildCount = x.ChildCount,
                GrandChildCount = x.GrandChildCount,
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                PlexLibraryId = x.PlexLibraryId,
                PlexServerId = x.PlexServerId,
                Type = PlexMediaType.TvShow,
                HasThumb = x.HasThumb,
                Qualities = x
                    .Qualities.OrderBy(quality => quality.VideoQuality)
                    .Select(quality => new PlexMediaQualityDTO
                    {
                        VideoQuality = quality.VideoQuality,
                        MediaDataType = PlexMediaType.TvShow,
                        DataId = -1,
                        MediaId = -1,
                    })
                    .ToList(),
            });

    private async Task<(List<int> Roles, List<int> Countries, List<int> Genres)> LoadTvShowPageMetadataAsync(
        HashSet<int> pageIds,
        CancellationToken cancellationToken
    )
    {
        var rows = await _dbContext
            .PlexTvShows.Where(x => pageIds.Contains(x.Id))
            .Select(x => new
            {
                Roles = x.Actors.Select(actor => actor.Id).ToList(),
                Countries = x.Countries.Select(country => country.Id).ToList(),
                Genres = x.Genres.Select(genre => genre.Id).ToList(),
            })
            .ToListAsync(cancellationToken);

        return (
            rows.SelectMany(x => x.Roles).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Countries).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Genres).Distinct().Order().ToList()
        );
    }

    private static (List<int> Roles, List<int> Countries, List<int> Genres) GetTvShowMetadata(
        IEnumerable<PlexTvShow> tvShows
    ) =>
        (
            tvShows.SelectMany(x => x.Actors).Select(x => x.Id).Distinct().Order().ToList(),
            tvShows.SelectMany(x => x.Countries).Select(x => x.Id).Distinct().Order().ToList(),
            tvShows.SelectMany(x => x.Genres).Select(x => x.Id).Distinct().Order().ToList()
        );
}
