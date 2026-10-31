using FastEndpoints;

namespace Reaparr.Data.Contracts;

public record GetMovieMediaByTypeCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;
