namespace Reaparr.Application;

public sealed record GetMediaOverviewCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMediaOverviewCommandValidator : AbstractValidator<GetMediaOverviewCommand>
{
    public GetMediaOverviewCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter.MediaType)
            .Must(x =>
                x
                    is PlexMediaType.Movie
                        or PlexMediaType.TvShow
                        or PlexMediaType.Music
                        or PlexMediaType.PhotoAlbum
                        or PlexMediaType.OtherVideos
            );
        RuleFor(x => x.Filter.PlexLibraryId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Filter.Parameters.Page).GreaterThan(0).When(x => x.Filter.Parameters.Page.HasValue);
        RuleFor(x => x.Filter.Parameters.PageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.Filter.Parameters.PageSize.HasValue);
        RuleFor(x => x.Filter.ComparisonState)
            .Null()
            .When(x => x.Filter.MediaType is not PlexMediaType.Movie and not PlexMediaType.TvShow);
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
            PlexMediaType.Music => await _commandExecutor.Send(
                new GetMediaOverviewMusicCommand(command.Filter),
                cancellationToken
            ),
            PlexMediaType.PhotoAlbum => await _commandExecutor.Send(
                new GetMediaOverviewPhotoCommand(command.Filter),
                cancellationToken
            ),
            PlexMediaType.OtherVideos => await _commandExecutor.Send(
                new GetMediaOverviewOtherVideoCommand(command.Filter),
                cancellationToken
            ),
            _ => Result.Fail("Media type {FilterMediaType} is not supported", command.Filter.MediaType),
        };

        return result.LogIfFailed();
    }
}
