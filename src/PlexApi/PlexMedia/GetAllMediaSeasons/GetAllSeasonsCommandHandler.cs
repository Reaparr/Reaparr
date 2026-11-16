namespace Reaparr.PlexApi;

public class GetAllSeasonsCommandHandler : ICommandHandler<GetAllMediaSeasonsCommand, Result<List<PlexTvShowSeason>>>
{
    private readonly ICommandExecutor _commandExecutor;

    public GetAllSeasonsCommandHandler(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<List<PlexTvShowSeason>>> ExecuteAsync(
        GetAllMediaSeasonsCommand command,
        CancellationToken ct
    )
    {
        var plexLibrary = command.PlexLibrary;

        var mediaListResult = await _commandExecutor.Send(
            new GetAllMediaByTypeFromPlexApiCommand(plexLibrary, PlexMediaType.Season),
            ct
        );

        if (mediaListResult.IsFailed)
            return mediaListResult.ToResult().LogIfFailed();

        var mediaList = mediaListResult.Value.ToPlexTvShowSeasons();
        return Result.Ok(mediaList);
    }
}
