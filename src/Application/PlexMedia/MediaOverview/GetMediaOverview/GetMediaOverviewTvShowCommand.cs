using FlexQuery.NET;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

public sealed record GetMediaOverviewTvShowCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMediaOverviewTvShowCommandValidator : AbstractValidator<GetMediaOverviewTvShowCommand>
{
    public GetMediaOverviewTvShowCommandValidator() => RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.TvShow);
}

public sealed class GetMediaOverviewTvShowCommandHandler
    : ICommandHandler<GetMediaOverviewTvShowCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ICommandExecutor _commandExecutor;

    public GetMediaOverviewTvShowCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        ICommandExecutor commandExecutor
    )
    {
        _dbContextFactory = dbContextFactory;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMediaOverviewTvShowCommand command,
        CancellationToken cancellationToken
    )
    {
        var filter = command.Filter;
        var options = QueryOptionsParser.Parse(filter.Parameters);
        var sort = options.ResolveSort();
        if (sort is null || filter.ComparisonState.HasValue)
        {
            var mediaResult = await _commandExecutor.Send(
                new GetMediaByTypeCommand { Filter = filter },
                cancellationToken
            );
            return mediaResult.LogIfFailed();
        }

        using var context = await _dbContextFactory.CreateAsync();
        var allowedLibraryIds = await context.ResolveAllowedLibraryIdsAsync(filter, cancellationToken);
        if (allowedLibraryIds.Count == 0)
        {
            return Result.Ok(
                new PagedMediaQueryResult
                {
                    QueryHash = filter.QueryHash,
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                }
            );
        }

        if (
            !await context.MediaOverviewTvShowSnapshots.AnyAsync(
                x => allowedLibraryIds.Contains(x.PlexLibraryId),
                cancellationToken
            )
        )
        {
            var mediaResult = await _commandExecutor.Send(
                new GetMediaByTypeCommand { Filter = filter },
                cancellationToken
            );
            return mediaResult.LogIfFailed();
        }

        var candidates = context.PlexTvShows.ApplyFilter(options);
        var snapshots = context.MediaOverviewTvShowSnapshots.Where(snapshot =>
            allowedLibraryIds.Contains(snapshot.PlexLibraryId)
            && candidates.Any(tvShow => tvShow.Id == snapshot.PlexTvShowId)
        );
        var ids = await ApplyOrder(snapshots, sort.Value.Field, sort.Value.Descending)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => x.PlexTvShowId)
            .ToListAsync(cancellationToken);
        var aggregate = await snapshots
            .Join(
                context.PlexTvShows,
                snapshot => snapshot.PlexTvShowId,
                tvShow => tvShow.Id,
                (_, tvShow) => tvShow.MediaSize
            )
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), MediaSize = group.Sum() })
            .FirstOrDefaultAsync(cancellationToken);
        var totalCount = aggregate?.Count ?? 0;
        var mediaSize = aggregate?.MediaSize ?? 0;
        var items = await context
            .PlexTvShows.Where(x => ids.Contains(x.Id))
            .Select(x => new PlexMediaSlimDTO
            {
                Id = x.Id,
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
                PlexApiRatingKey = x.PlexApiRatingKey,
                PlexApiMetaDataKey = x.PlexApiMetaDataKey,
                Qualities = x
                    .Qualities.OrderBy(quality => quality.Quality)
                    .Select(quality => new PlexMediaQualityDTO
                    {
                        Quality = quality.Quality,
                        MediaDataType = PlexMediaType.TvShow,
                        DataId = quality.Id,
                        MediaId = x.Id,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
        items = items.RestorePageOrder(ids);

        var result = await context.CreatePageResultAsync(
            filter,
            options,
            allowedLibraryIds,
            items,
            totalCount,
            mediaSize,
            cancellationToken
        );
        if (items.Count > 0)
        {
            var pageIds = items.Select(x => x.Id).AsEnumerable();
            var metadata = await context
                .PlexTvShowActors.Where(x => pageIds.Contains(x.PlexTvShowId))
                .Select(x => new { Type = 0, Id = x.PlexActorId })
                .Concat(
                    context
                        .PlexTvShowCountries.Where(x => pageIds.Contains(x.PlexTvShowId))
                        .Select(x => new { Type = 1, Id = x.CountryId })
                )
                .Concat(
                    context
                        .PlexTvShowGenres.Where(x => pageIds.Contains(x.PlexTvShowId))
                        .Select(x => new { Type = 2, Id = x.GenresId })
                )
                .Distinct()
                .ToListAsync(cancellationToken);
            result.Roles = metadata.Where(x => x.Type == 0).Select(x => x.Id).OrderBy(x => x).ToList();
            result.Countries = metadata.Where(x => x.Type == 1).Select(x => x.Id).OrderBy(x => x).ToList();
            result.Genres = metadata.Where(x => x.Type == 2).Select(x => x.Id).OrderBy(x => x).ToList();
        }
        result.Qualities = items
            .SelectMany(x => x.Qualities)
            .Select(x => x.Quality.ToId())
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        return Result.Ok(result);
    }

    private static IQueryable<MediaOverviewTvShowSnapshot> ApplyOrder(
        IQueryable<MediaOverviewTvShowSnapshot> query,
        string field,
        bool descending
    ) =>
        field switch
        {
            nameof(BasePlexMedia.Year) => descending
                ? query.OrderByDescending(x => x.YearRank)
                : query.OrderBy(x => x.YearRank),
            nameof(BasePlexMedia.AddedAt) => descending
                ? query.OrderByDescending(x => x.AddedAtRank)
                : query.OrderBy(x => x.AddedAtRank),
            nameof(BasePlexMedia.UpdatedAt) => descending
                ? query.OrderByDescending(x => x.UpdatedAtRank)
                : query.OrderBy(x => x.UpdatedAtRank),
            nameof(BasePlexMedia.Duration) => descending
                ? query.OrderByDescending(x => x.DurationRank)
                : query.OrderBy(x => x.DurationRank),
            nameof(BasePlexMedia.MediaSize) => descending
                ? query.OrderByDescending(x => x.MediaSizeRank)
                : query.OrderBy(x => x.MediaSizeRank),
            "Quality" => descending ? query.OrderByDescending(x => x.QualityRank) : query.OrderBy(x => x.QualityRank),
            _ => descending ? query.OrderByDescending(x => x.TitleRank) : query.OrderBy(x => x.TitleRank),
        };
}
