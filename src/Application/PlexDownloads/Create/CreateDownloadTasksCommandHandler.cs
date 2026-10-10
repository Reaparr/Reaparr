namespace Reaparr.Application;

public class CreateDownloadTasksCommandValidator : AbstractValidator<CreateDownloadTasksCommand>
{
    public CreateDownloadTasksCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Request)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.Request.DownloadMedias).NotEmpty();
                RuleForEach(x => x.Request.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
            });
    }
}

public class CreateDownloadTasksCommandHandler
    : ICommandHandler<CreateDownloadTasksCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly IEventPublisher _eventPublisher;
    private readonly INotificationHubService _notificationHubService;

    public CreateDownloadTasksCommandHandler(
        ICommandExecutor commandExecutor,
        IEventPublisher eventPublisher,
        INotificationHubService notificationHubService
    )
    {
        _commandExecutor = commandExecutor;
        _eventPublisher = eventPublisher;
        _notificationHubService = notificationHubService;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        CreateDownloadTasksCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        var downloadMedias = command.Request.DownloadMedias;
        var report = new DownloadTaskCreationReport();

        if (downloadMedias.Any(x => x.Type == PlexMediaType.Movie))
        {
            var result = await _commandExecutor.Send(new GenerateDownloadTaskMoviesCommand(request), cancellationToken);
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (downloadMedias.Any(x => x.Type == PlexMediaType.TvShow))
        {
            var result = await _commandExecutor.Send(
                new GenerateDownloadTaskTvShowsCommand(request),
                cancellationToken
            );
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (downloadMedias.Any(x => x.Type == PlexMediaType.Season))
        {
            var result = await _commandExecutor.Send(
                new GenerateDownloadTaskTvShowSeasonsCommand(request),
                cancellationToken
            );
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (downloadMedias.Any(x => x.Type == PlexMediaType.Episode))
        {
            var result = await _commandExecutor.Send(
                new GenerateDownloadTaskTvShowEpisodesCommand(request),
                cancellationToken
            );
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (downloadMedias.Any(x => x.Type == PlexMediaType.MusicArtist))
        {
            var result = await _commandExecutor.Send(
                new GenerateDownloadTaskMusicArtistsCommand(request),
                cancellationToken
            );
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (downloadMedias.Any(x => x.Type == PlexMediaType.MusicAlbum))
        {
            var result = await _commandExecutor.Send(
                new GenerateDownloadTaskMusicAlbumsCommand(request),
                cancellationToken
            );
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (downloadMedias.Any(x => x.Type == PlexMediaType.MusicTrack))
        {
            var result = await _commandExecutor.Send(
                new GenerateDownloadTaskMusicTracksCommand(request),
                cancellationToken
            );
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (downloadMedias.Any(x => x.Type == PlexMediaType.PhotoAlbum))
        {
            var result = await _commandExecutor.Send(
                new GenerateDownloadTaskPhotoAlbumsCommand(request),
                cancellationToken
            );
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (downloadMedias.Any(x => x.Type == PlexMediaType.PhotoImage))
        {
            var result = await _commandExecutor.Send(
                new GenerateDownloadTaskPhotoImagesCommand(request),
                cancellationToken
            );
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (downloadMedias.Any(x => x.Type == PlexMediaType.OtherVideos))
        {
            var result = await _commandExecutor.Send(
                new GenerateDownloadTaskOtherVideosCommand(request),
                cancellationToken
            );
            if (result.IsFailed)
                return result.LogIfFailed();
            report += result.Value;
        }

        if (report.Total > 0)
        {
            // Notify the DownloadQueue to check for new tasks in the PlexSevers with new DownloadTasks
            var uniquePlexServers = request
                .DownloadMedias.MergeAndGroupList()
                .Select(x => x.PlexServerId)
                .Distinct()
                .ToList();

            await _eventPublisher.PublishAsync(new CheckDownloadQueueEvent(uniquePlexServers), cancellationToken);

            await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.DownloadTasks]);
        }

        return Result.Ok(report);
    }
}
