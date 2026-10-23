using FlexQuery.NET;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;
using static Reaparr.Data.MediaByTypeQuerySupport;

namespace Reaparr.Data;

public sealed class GetPhotoAlbumMediaByTypeCommandValidator : AbstractValidator<GetPhotoAlbumMediaByTypeCommand>
{
    public GetPhotoAlbumMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter)
            .NotNull()
            .SetValidator(new MediaQueryFilterValidator(PlexMediaType.PhotoAlbum));
    }
}

public sealed class GetPhotoAlbumMediaByTypeCommandHandler
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
        foreach (var sort in options.Sort)
        {
            if (sort.Field == SortType.Title.ToSortValue())
                sort.Field = nameof(PlexMediaSlimDTO.Title);
        }

        if (options.Sort.Count == 0)
            options.Sort.Add(new SortNode { Field = nameof(PlexMediaSlimDTO.Title), Descending = false });

        options.Sort.Add(new SortNode { Field = nameof(PlexMediaSlimDTO.Id), Descending = false });

        var albumQuery = _dbContext.PlexPhotoAlbums
            .ProjectToMediaSlimDTO()
            .ApplyFilter(options)
            .ApplySort(options);

        response.TotalCount = await albumQuery.CountAsync(ct);
        response.MediaSize = await _dbContext
            .PlexPhotoMediaData.Where(file =>
                file.AlbumMemberships.Any(edge => albumQuery.Any(album => album.Id == edge.PlexPhotoAlbumId))
            )
            .SumAsync(file => file.Size, ct);
        response.TotalMediaSize = response.MediaSize;

        await SetNavigationIndexes(response, albumQuery.ProjectNavigationIndexRows(), options, ct);
        response.Items = await albumQuery.ApplyPaging(options).ToListAsync(ct);
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
}
