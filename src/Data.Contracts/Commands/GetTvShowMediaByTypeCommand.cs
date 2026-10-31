using FastEndpoints;

namespace Reaparr.Data.Contracts;

public record GetTvShowMediaByTypeCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;
