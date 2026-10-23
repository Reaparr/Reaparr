using FastEndpoints;

namespace Reaparr.Data.Contracts;

public record GetPhotoAlbumMediaByTypeCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;
