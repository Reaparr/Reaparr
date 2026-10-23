using FlexQuery.NET;
using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;
using static Reaparr.Data.MediaByTypeQuerySupport;

namespace Reaparr.Data;

public sealed class GetOtherVideoMediaByTypeCommandValidator : AbstractValidator<GetOtherVideoMediaByTypeCommand>
{
    public GetOtherVideoMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter).NotNull().SetValidator(new MediaQueryFilterValidator(PlexMediaType.OtherVideos));
    }
}

public sealed class GetOtherVideoMediaByTypeCommandHandler
    : ICommandHandler<GetOtherVideoMediaByTypeCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContext _dbContext;

    public GetOtherVideoMediaByTypeCommandHandler(IReaparrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetOtherVideoMediaByTypeCommand command,
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

        var videoQuery = _dbContext.PlexOtherVideos.ApplyFilter(options).ApplySort(options);
        response.TotalCount = await videoQuery.CountAsync(cancellationToken);
        response.MediaSize = await videoQuery.SumAsync(x => x.MediaSize, cancellationToken);
        response.TotalMediaSize = response.MediaSize;

        await SetNavigationIndexes(
            response,
            videoQuery.ProjectOtherVideoNavigationIndexRows(),
            options,
            cancellationToken
        );

        var pageCandidates = await videoQuery
            .ApplyPaging(options)
            .ProjectOtherVideoPage()
            .ToListAsync(cancellationToken);
        var pageMetadata = await LoadOtherVideoPageMetadataAsync(
            pageCandidates.Select(x => x.Id).ToHashSet(),
            cancellationToken
        );
        SetComparisonPage(response, pageCandidates, pageMetadata, includeQuality: true);

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

    private async Task<(List<int> Roles, List<int> Countries, List<int> Genres)> LoadOtherVideoPageMetadataAsync(
        HashSet<int> pageIds,
        CancellationToken cancellationToken
    )
    {
        var rows = await _dbContext
            .PlexOtherVideos.Where(x => pageIds.Contains(x.Id))
            .ProjectOtherVideoMetadataRows()
            .ToListAsync(cancellationToken);

        return (
            rows.SelectMany(x => x.Roles).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Countries).Distinct().Order().ToList(),
            rows.SelectMany(x => x.Genres).Distinct().Order().ToList()
        );
    }
}
