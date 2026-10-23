using FlexQuery.NET;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;
using static Reaparr.Data.MediaByTypeQuerySupport;

namespace Reaparr.Data;

public record GetPhotoAlbumMediaByTypeCommand(MediaQueryFilter Filter, QueryOptions Options)
    : ICommand<Result<PagedMediaQueryResult>>;

public class GetPhotoAlbumMediaByTypeCommandValidator : AbstractValidator<GetPhotoAlbumMediaByTypeCommand>
{
    public GetPhotoAlbumMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.PhotoAlbum).When(x => x.Filter is not null);
    }
}

public class GetPhotoAlbumMediaByTypeCommandHandler
    : ICommandHandler<GetPhotoAlbumMediaByTypeCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContext _dbContext;

    public GetPhotoAlbumMediaByTypeCommandHandler(IReaparrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetPhotoAlbumMediaByTypeCommand command,
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
        var response = CreateResponse(filter);

        foreach (var sort in options.Sort)
        {
            if (sort.Field == SortType.Title.ToSortValue())
                sort.Field = nameof(PlexMediaSlimDTO.Title);
        }

        if (options.Sort.Count == 0)
            options.Sort.Add(new SortNode { Field = nameof(PlexMediaSlimDTO.Title), Descending = false });

        options.Sort.Add(new SortNode { Field = nameof(PlexMediaSlimDTO.Id), Descending = false });

        var albumQuery = _dbContext.PlexPhotoAlbums.ProjectToMediaSlimDTO().ApplyFilter(options).ApplySort(options);

        response.TotalCount = await albumQuery.CountAsync(ct);
        response.MediaSize = await _dbContext
            .PlexPhotoMediaData.Where(file =>
                file.AlbumMemberships.Any(edge => albumQuery.Any(album => album.Id == edge.PlexPhotoAlbumId))
            )
            .SumAsync(file => file.Size, ct);
        response.TotalMediaSize = response.MediaSize;

        await SetNavigationIndexes(
            response,
            albumQuery.Select(x => new MediaNavigationIndexRow
            {
                SearchTitle = x.SearchTitle,
                Year = x.Year,
                QualityValue = null,
                Duration = x.Duration,
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                MediaSize = x.MediaSize,
            }),
            options,
            ct
        );

        response.Items = await albumQuery.ApplyPaging(options).ToListAsync(ct);
        return Result.Ok(response);
    }
}
