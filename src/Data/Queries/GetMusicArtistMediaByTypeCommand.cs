using FlexQuery.NET;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;
using static Reaparr.Data.MediaByTypeQuerySupport;

namespace Reaparr.Data;

public record GetMusicArtistMediaByTypeCommand(MediaQueryFilter Filter, QueryOptions Options)
    : ICommand<Result<PagedMediaQueryResult>>;

public class GetMusicArtistMediaByTypeCommandValidator : AbstractValidator<GetMusicArtistMediaByTypeCommand>
{
    public GetMusicArtistMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.MusicArtist).When(x => x.Filter is not null);
    }
}

public class GetMusicArtistMediaByTypeCommandHandler
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
        var options = command.Options;
        options.Sort.RemoveAll(static sort =>
        {
            var field = sort.Field.ToSortType();
            return field is SortType.Year or SortType.VideoQuality;
        });
        if (options.Sort.Count == 0 && filter.PlexLibraryId > 0)
            options.Sort.Add(new SortNode { Field = SortType.Title.ToSortValue(), Descending = false });
        var response = CreateResponse(filter);

        if (options.Sort.Any(sort => sort.Field.ToSortType() == SortType.ComparisonState))
        {
            var candidateResult = await LoadComparisonCandidatesAsync(
                ProjectMusicArtistComparisonCandidates(options),
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
            var metadata = await LoadMusicArtistPageMetadataAsync(pageCandidates, ct);
            SetComparisonPage(response, pageCandidates, metadata, includeQuality: false);
            return Result.Ok(response);
        }

        var artistQuery = _dbContext
            .PlexMusicArtists.Include(x => x.Albums)
            .Include(x => x.Actors)
            .Include(x => x.Countries)
            .Include(x => x.Genres)
            .Include(x => x.Qualities)
            .ApplyFilter(options)
            .ApplySort(options);

        List<PlexMusicArtist> artists;
        if (filter.ComparisonState.HasValue)
        {
            var candidates = await artistQuery.ToListAsync(ct);
            var candidateDtos = candidates.Select(x => x.ToSlimDTOMapper()).ToList();
            var comparisonResult = await ApplyComparisonStateAsync(_commandExecutor, candidateDtos, filter, ct);
            if (comparisonResult.IsCancelledOrFailed)
                return comparisonResult.ToResult<PagedMediaQueryResult>();

            var filteredDtos = candidateDtos
                .Where(x => x.ComparisonId == filter.ComparisonState.Value.ToComparisonId())
                .ToList();
            var filteredIds = filteredDtos.Select(x => x.Id).ToHashSet();
            var filteredArtists = candidates.Where(x => filteredIds.Contains(x.Id)).ToList();

            response.TotalCount = filteredDtos.Count;
            response.MediaSize = filteredDtos.Sum(x => x.MediaSize);
            response.TotalMediaSize = response.MediaSize;
            response.NavigationIndexes = MediaNavigationIndexBuilder.Build(
                filteredArtists.Select(x => new MediaNavigationIndexRow
                {
                    SearchTitle = x.SearchTitle,
                    Year = 0,
                    QualityValue = null,
                    Duration = x.Duration,
                    AddedAt = x.AddedAt,
                    UpdatedAt = x.UpdatedAt,
                    MediaSize = x.MediaSize,
                }),
                ((IReadOnlyList<SortNode>)options.Sort).Count == 0
                    ? SortType.Title
                    : ((IReadOnlyList<SortNode>)options.Sort)[0].Field.ToSortType()
            );

            response.Items = ApplyDtoPaging(filteredDtos, response.Page, response.PageSize);
            var pagedIds = response.Items.Select(x => x.Id).ToHashSet();
            artists = filteredArtists.Where(x => pagedIds.Contains(x.Id)).ToList();
        }
        else
        {
            response.TotalCount = await artistQuery.CountAsync(ct);
            response.MediaSize = await artistQuery.SumAsync(x => x.MediaSize, ct);
            response.TotalMediaSize = response.MediaSize;
            await SetNavigationIndexes(
                response,
                artistQuery.Select(x => new MediaNavigationIndexRow
                {
                    SearchTitle = x.SearchTitle,
                    Year = 0,
                    QualityValue = null,
                    Duration = x.Duration,
                    AddedAt = x.AddedAt,
                    UpdatedAt = x.UpdatedAt,
                    MediaSize = x.MediaSize,
                }),
                options,
                ct
            );

            artists = await artistQuery.ApplyPaging(options).ToListAsync(ct);
            response.Items = artists.Select(x => x.ToSlimDTOMapper()).ToList();
            var comparisonResult = await ApplyComparisonStateAsync(_commandExecutor, response.Items, filter, ct);
            if (comparisonResult.IsCancelledOrFailed)
                return comparisonResult.ToResult<PagedMediaQueryResult>();
        }

        response.Roles = artists.SelectMany(x => x.Actors).Select(x => x.Id).Distinct().Order().ToList();
        response.Countries = artists.SelectMany(x => x.Countries).Select(x => x.Id).Distinct().Order().ToList();
        response.Genres = artists.SelectMany(x => x.Genres).Select(x => x.Id).Distinct().Order().ToList();
        return Result.Ok(response);
    }

    private IQueryable<PlexMediaSlimDTO> ProjectMusicArtistComparisonCandidates(QueryOptions options) =>
        _dbContext
            .PlexMusicArtists.ApplyFilter(options)
            .Select(x => new PlexMediaSlimDTO
            {
                Id = x.Id,
                PlexApiRatingKey = x.PlexApiRatingKey,
                PlexApiMetaDataKey = x.PlexApiMetaDataKey,
                Title = x.Title,
                SearchTitle = x.SearchTitle,
                SortIndex = x.SortIndex,
                Year = 0,
                Duration = x.Duration,
                MediaSize = x.MediaSize,
                ChildCount = x.ChildCount,
                GrandChildCount = x.Albums.Sum(album => album.ChildCount),
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                PlexLibraryId = x.PlexLibraryId,
                PlexServerId = x.PlexServerId,
                Type = PlexMediaType.MusicArtist,
                HasThumb = x.HasThumb,
                Qualities = x
                    .Qualities.OrderBy(quality => quality.Id)
                    .Select(quality => new PlexMediaQualityDTO
                    {
                        VideoQuality = VideoQuality.None,
                        AudioQuality = quality.AudioQuality,
                        MediaDataType = PlexMediaType.MusicArtist,
                        DataId = -1,
                        MediaId = quality.PlexMusicArtistId,
                    })
                    .ToList(),
            });

    private async Task<(List<int> Roles, List<int> Countries, List<int> Genres)> LoadMusicArtistPageMetadataAsync(
        IReadOnlyList<PlexMediaSlimDTO> pageCandidates,
        CancellationToken ct
    )
    {
        var pageIds = pageCandidates.Select(x => x.Id).ToHashSet();
        var rows = await _dbContext
            .PlexMusicArtists.Where(x => pageIds.Contains(x.Id))
            .Select(x => new
            {
                Roles = x.Actors.Select(actor => actor.Id).ToList(),
                Countries = x.Countries.Select(country => country.Id).ToList(),
                Genres = x.Genres.Select(genre => genre.Id).ToList(),
            })
            .ToListAsync(ct);

        return (
            rows.SelectMany(x => x.Roles).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Countries).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Genres).Distinct().Order().ToList()
        );
    }
}
