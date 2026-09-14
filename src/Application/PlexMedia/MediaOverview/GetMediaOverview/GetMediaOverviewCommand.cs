namespace Reaparr.Application;

public sealed record GetMediaOverviewCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMediaOverviewCommandValidator : AbstractValidator<GetMediaOverviewCommand>
{
    public GetMediaOverviewCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter.MediaType).Must(x => x is PlexMediaType.Movie or PlexMediaType.TvShow);
        RuleFor(x => x.Filter.PlexLibraryId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Filter.Parameters.Page).GreaterThan(0).When(x => x.Filter.Parameters.Page.HasValue);
        RuleFor(x => x.Filter.Parameters.PageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.Filter.Parameters.PageSize.HasValue);
    }
}

public sealed class GetMediaOverviewCommandHandler
    : ICommandHandler<GetMediaOverviewCommand, Result<PagedMediaQueryResult>>
{
    private readonly ICommandExecutor _commandExecutor;

    public GetMediaOverviewCommandHandler(ICommandExecutor commandExecutor) => _commandExecutor = commandExecutor;

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMediaOverviewCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = command.Filter.MediaType switch
        {
            PlexMediaType.Movie => await _commandExecutor.Send(
                new GetMediaOverviewMovieCommand(command.Filter),
                cancellationToken
            ),
            PlexMediaType.TvShow => await _commandExecutor.Send(
                new GetMediaOverviewTvShowCommand(command.Filter),
                cancellationToken
            ),
            _ => Result.Fail("Media type {FilterMediaType} is not supported", command.Filter.MediaType),
        };

        return result.LogIfFailed();
    }
}
