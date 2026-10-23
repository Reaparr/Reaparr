namespace Reaparr.BaseTests;

public partial class BaseUnitTest
{
    protected List<ICommand<Result<PagedMediaQueryResult>>> MediaByTypeDispatchedCommands { get; } = [];

    /// <summary>
    /// Routes the media-by-type orchestrator's per-media commands to their real handlers while retaining this test's
    /// database and command-executor mocks for comparison-state projection.
    /// </summary>
    protected void SetupMediaByTypeCommandDispatch()
    {
        var commandExecutor = Mock.Mock<ICommandExecutor>().Object;
        var executorMock = Mock.Mock<ICommandExecutor>();

        executorMock
            .Setup(x => x.Send(It.IsAny<GetMovieMediaByTypeCommand>(), It.IsAny<CancellationToken>()))
            .Returns((ICommand<Result<PagedMediaQueryResult>> command, CancellationToken ct) =>
            {
                var mediaCommand = (GetMovieMediaByTypeCommand)command;
                MediaByTypeDispatchedCommands.Add(mediaCommand);
                return new GetMovieMediaByTypeCommandHandler(IDbContext, commandExecutor).ExecuteAsync(
                    mediaCommand,
                    ct
                );
            });

        executorMock
            .Setup(x => x.Send(It.IsAny<GetTvShowMediaByTypeCommand>(), It.IsAny<CancellationToken>()))
            .Returns((ICommand<Result<PagedMediaQueryResult>> command, CancellationToken ct) =>
            {
                var mediaCommand = (GetTvShowMediaByTypeCommand)command;
                MediaByTypeDispatchedCommands.Add(mediaCommand);
                return new GetTvShowMediaByTypeCommandHandler(IDbContext, commandExecutor).ExecuteAsync(
                    mediaCommand,
                    ct
                );
            });

        executorMock
            .Setup(x => x.Send(It.IsAny<GetMusicArtistMediaByTypeCommand>(), It.IsAny<CancellationToken>()))
            .Returns((ICommand<Result<PagedMediaQueryResult>> command, CancellationToken ct) =>
            {
                var mediaCommand = (GetMusicArtistMediaByTypeCommand)command;
                MediaByTypeDispatchedCommands.Add(mediaCommand);
                return new GetMusicArtistMediaByTypeCommandHandler(IDbContext, commandExecutor).ExecuteAsync(
                    mediaCommand,
                    ct
                );
            });

        executorMock
            .Setup(x => x.Send(It.IsAny<GetPhotoAlbumMediaByTypeCommand>(), It.IsAny<CancellationToken>()))
            .Returns((ICommand<Result<PagedMediaQueryResult>> command, CancellationToken ct) =>
            {
                var mediaCommand = (GetPhotoAlbumMediaByTypeCommand)command;
                MediaByTypeDispatchedCommands.Add(mediaCommand);
                return new GetPhotoAlbumMediaByTypeCommandHandler(IDbContext).ExecuteAsync(
                    mediaCommand,
                    ct
                );
            });

        executorMock
            .Setup(x => x.Send(It.IsAny<GetOtherVideoMediaByTypeCommand>(), It.IsAny<CancellationToken>()))
            .Returns((ICommand<Result<PagedMediaQueryResult>> command, CancellationToken ct) =>
            {
                var mediaCommand = (GetOtherVideoMediaByTypeCommand)command;
                MediaByTypeDispatchedCommands.Add(mediaCommand);
                return new GetOtherVideoMediaByTypeCommandHandler(IDbContext).ExecuteAsync(
                    mediaCommand,
                    ct
                );
            });
    }
}
