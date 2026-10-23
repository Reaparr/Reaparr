using FlexQuery.NET;
using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;
using static Reaparr.Data.MediaByTypeQuerySupport;

namespace Reaparr.Data;

public record GetMovieMediaByTypeCommand(MediaQueryFilter Filter, QueryOptions Options)
    : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMovieMediaByTypeCommandValidator : AbstractValidator<GetMovieMediaByTypeCommand>
{
    public GetMovieMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.Movie).When(x => x.Filter is not null);
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
        var options = command.Options;
        var response = CreateResponse(filter);

        if (options.Sort.Any(sort => sort.Field.ToSortType() == SortType.ComparisonState))
        {
            var candidatesResult = await LoadComparisonCandidatesAsync(
                ProjectMovieComparisonCandidates(options),
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
            return Result.Ok(response);
        }
        var movieQuery = _dbContext
            .PlexMovies.IncludeMediaData()
            .Include(x => x.Actors)
            .Include(x => x.Countries)
            .Include(x => x.Genres)
            .ApplyFilter(options)
            .ApplySort(options);

        if (filter.ComparisonState.HasValue)
        {
            var movies = await movieQuery.ToListAsync(cancellationToken);
            var movieDtos = movies.Select(x => x.ToSlimDTO()).ToList();
            var comparisonResult = await ApplyComparisonStateAsync(
                _commandExecutor,
                movieDtos,
                filter,
                cancellationToken
            );
            if (comparisonResult.IsCancelledOrFailed)
                return comparisonResult.ToResult<PagedMediaQueryResult>();

            var filteredDtos = movieDtos
                .Where(x => x.ComparisonId == filter.ComparisonState.Value.ToComparisonId())
                .ToList();
            var filteredIds = filteredDtos.Select(x => x.Id).ToHashSet();
            var filteredMovies = movies.Where(x => filteredIds.Contains(x.Id)).ToList();

            response.NavigationIndexes = MediaNavigationIndexBuilder.Build(
                filteredMovies.Select(x => new MediaNavigationIndexRow
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
            var pagedMovies = filteredMovies.Where(x => pagedIds.Contains(x.Id)).ToList();
            SetComparisonPage(response, pagedDtos, GetMovieMetadata(pagedMovies), includeQuality: true);
        }
        else
        {
            response.TotalCount = await movieQuery.CountAsync(cancellationToken);
            response.MediaSize = await movieQuery.SumAsync(x => x.MediaSize, cancellationToken);
            response.TotalMediaSize = response.MediaSize;

            await SetNavigationIndexes(
                response,
                movieQuery.Select(x => new MediaNavigationIndexRow
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

            var movies = await movieQuery.ApplyPaging(options).ToListAsync(cancellationToken);
            var movieDtos = movies.Select(x => x.ToSlimDTO()).ToList();
            var comparisonResult = await ApplyComparisonStateAsync(
                _commandExecutor,
                movieDtos,
                filter,
                cancellationToken
            );
            if (comparisonResult.IsCancelledOrFailed)
                return comparisonResult.ToResult<PagedMediaQueryResult>();

            SetComparisonPage(response, movieDtos, GetMovieMetadata(movies), includeQuality: true);
        }

        return Result.Ok(response);
    }

    private IQueryable<PlexMediaSlimDTO> ProjectMovieComparisonCandidates(QueryOptions options) =>
        _dbContext
            .PlexMovies.ApplyFilter(options)
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
                GrandChildCount = 0,
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                PlexLibraryId = x.PlexLibraryId,
                PlexServerId = x.PlexServerId,
                Type = PlexMediaType.Movie,
                HasThumb = x.HasThumb,
                Qualities = x
                    .MediaDataList.OrderBy(data => data.VideoQuality)
                    .Select(data => new PlexMediaQualityDTO
                    {
                        VideoQuality = data.VideoQuality,
                        MediaDataType = PlexMediaType.Movie,
                        DataId = data.Id,
                        MediaId = data.PlexMovieId,
                    })
                    .ToList(),
            });

    private async Task<(List<int> Roles, List<int> Countries, List<int> Genres)> LoadMoviePageMetadataAsync(
        HashSet<int> pageIds,
        CancellationToken cancellationToken
    )
    {
        var rows = await _dbContext
            .PlexMovies.Where(x => pageIds.Contains(x.Id))
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

    private static (List<int> Roles, List<int> Countries, List<int> Genres) GetMovieMetadata(
        IEnumerable<PlexMovie> movies
    ) =>
        (
            movies.SelectMany(x => x.Actors).Select(x => x.Id).Distinct().Order().ToList(),
            movies.SelectMany(x => x.Countries).Select(x => x.Id).Distinct().Order().ToList(),
            movies.SelectMany(x => x.Genres).Select(x => x.Id).Distinct().Order().ToList()
        );
}
