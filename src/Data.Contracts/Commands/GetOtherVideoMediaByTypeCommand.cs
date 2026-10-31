using FastEndpoints;

namespace Reaparr.Data.Contracts;

public record GetOtherVideoMediaByTypeCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;
