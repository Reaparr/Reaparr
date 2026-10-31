using FastEndpoints;

namespace Reaparr.Data.Contracts;

public record GetMusicArtistMediaByTypeCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;
