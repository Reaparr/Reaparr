using FlexQuery.NET;
using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;
using static Reaparr.Data.MediaByTypeQuerySupport;

namespace Reaparr.Data;

public record GetOtherVideoMediaByTypeCommand(MediaQueryFilter Filter, QueryOptions Options)
    : ICommand<Result<PagedMediaQueryResult>>;

public class GetOtherVideoMediaByTypeCommandValidator : AbstractValidator<GetOtherVideoMediaByTypeCommand>
{
    public GetOtherVideoMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.OtherVideos).When(x => x.Filter is not null);
    }
}

public class GetOtherVideoMediaByTypeCommandHandler
    : ICommandHandler<GetOtherVideoMediaByTypeCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContext _dbContext;

    public GetOtherVideoMediaByTypeCommandHandler(IReaparrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetOtherVideoMediaByTypeCommand command,
        CancellationToken ct
    )
    {
        var filter = command.Filter;
        var options = command.Options;
        var response = CreateResponse(filter);

        var videoQuery = _dbContext
            .PlexOtherVideos.Include(x => x.MediaDataList)
            .Include(x => x.Actors)
            .Include(x => x.Countries)
            .Include(x => x.Genres)
            .ApplyFilter(options)
            .ApplySort(options);

        response.TotalCount = await videoQuery.CountAsync(ct);
        response.MediaSize = await videoQuery.SumAsync(x => x.MediaSize, ct);
        response.TotalMediaSize = response.MediaSize;
        await SetNavigationIndexes(
            response,
            videoQuery.Select(x => new MediaNavigationIndexRow
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
            ct
        );

        var videos = await videoQuery.ApplyPaging(options).ToListAsync(ct);
        response.Items = videos.Select(x => x.ToSlimDTOMapper()).ToList();
        response.Roles = videos.SelectMany(x => x.Actors).Select(x => x.Id).Distinct().Order().ToList();
        response.Countries = videos.SelectMany(x => x.Countries).Select(x => x.Id).Distinct().Order().ToList();
        response.Genres = videos.SelectMany(x => x.Genres).Select(x => x.Id).Distinct().Order().ToList();
        response.Qualities = response
            .Items.SelectMany(x => x.Qualities)
            .Select(x => x.VideoQuality.ToId())
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        return Result.Ok(response);
    }
}
