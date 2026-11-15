namespace Reaparr.Application.UnitTests;

public class GetDownloadPreviewQueryCommandUnitTests : BaseCommandUnitTest<GetDownloadPreviewQuery>
{
    [Test]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldRejectWrongMediaScope_WhenNonPhotoSelectionUsesAnotherServer(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(
            62830,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMusicLibraryCount = type == PlexMediaType.MusicTrack ? 1 : 0;
                config.MusicArtistCount = type == PlexMediaType.MusicTrack ? 1 : 0;
                config.MusicAlbumCount = type == PlexMediaType.MusicTrack ? 1 : 0;
                config.MusicTrackCount = type == PlexMediaType.MusicTrack ? 2 : 0;
                config.PlexOtherVideoLibraryCount = type == PlexMediaType.OtherVideos ? 1 : 0;
                config.OtherVideoCount = type == PlexMediaType.OtherVideos ? 2 : 0;
            }
        );
        var dbContext = IDbContext;
        List<BasePlexMedia> media =
            type == PlexMediaType.MusicTrack
                ? [.. await dbContext.PlexMusicTracks.OrderBy(x => x.Id).ToListAsync(CancellationToken)]
                : [.. await dbContext.PlexOtherVideos.OrderBy(x => x.Id).ToListAsync(CancellationToken)];
        var target = media[0];
        var foreignServerId = await dbContext
            .PlexServers.Where(x => x.Id != target.PlexServerId)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);
        var query = new GetDownloadPreviewQuery(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    new DownloadMediaDTO
                    {
                        Type = type,
                        SelectionKind = null,
                        PlexServerId = foreignServerId,
                        PlexLibraryId = target.PlexLibraryId,
                        MediaIds = [target.Id],
                        Qualities = [],
                    },
                ],
            }
        );
        SetupFamilyCommandExecutor();

        // Act
        var result = await TestHandlerExecuteAsync<List<DownloadPreview>>(query);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.ToResult().Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnNoPreview_WhenEmptyListIsGiven()
    {
        // Arrange
        await SetupDatabase(15140);

        // Act
        var result = await new GetDownloadPreviewQueryHandler(Mock.Mock<ICommandExecutor>().Object).ExecuteAsync(
            new GetDownloadPreviewQuery(new DownloadPreviewRequest { DownloadMedias = [] }),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldBuildMixedMovieAndTvHierarchy()
    {
        // Arrange
        await SetupDatabase(
            47561,
            config =>
            {
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 2;
            }
        );
        var dbContext = IDbContext;
        var movies = await dbContext.PlexMovies.ToListAsync(CancellationToken);
        var shows = await dbContext.PlexTvShows.ToListAsync(CancellationToken);
        var query = new GetDownloadPreviewQuery(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    new DownloadMediaDTO
                    {
                        Type = PlexMediaType.Movie,
                        SelectionKind = null,
                        PlexServerId = movies[0].PlexServerId,
                        PlexLibraryId = movies[0].PlexLibraryId,
                        MediaIds = movies.Select(x => x.Id).ToList(),
                        Qualities = [],
                    },
                    new DownloadMediaDTO
                    {
                        Type = PlexMediaType.TvShow,
                        SelectionKind = null,
                        PlexServerId = shows[0].PlexServerId,
                        PlexLibraryId = shows[0].PlexLibraryId,
                        MediaIds = shows.Select(x => x.Id).ToList(),
                        Qualities = [],
                    },
                ],
            }
        );
        SetupFamilyCommandExecutor();

        // Act
        var result = await TestHandlerExecuteAsync<List<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(5);
        result.Value.Count(x => x.MediaType == PlexMediaType.Movie).ShouldBe(3);
        result.Value.Count(x => x.MediaType == PlexMediaType.TvShow).ShouldBe(2);
        result
            .Value.Where(x => x.MediaType == PlexMediaType.TvShow)
            .ShouldAllBe(x => x.Children.Count == 2 && x.Children.All(season => season.Children.Count == 2));
    }

    [Test]
    public async Task ShouldHandleMultipleMovieLibrariesAndServers()
    {
        // Arrange
        await SetupDatabase(
            11223,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
                config.MovieCount = 2;
            }
        );
        var movies = await IDbContext.PlexMovies.AsNoTracking().ToListAsync(CancellationToken);
        var query = new GetDownloadPreviewQuery(
            new DownloadPreviewRequest
            {
                DownloadMedias = movies
                    .GroupBy(x => (x.PlexServerId, x.PlexLibraryId))
                    .Select(group => new DownloadMediaDTO
                    {
                        Type = PlexMediaType.Movie,
                        SelectionKind = null,
                        PlexServerId = group.Key.PlexServerId,
                        PlexLibraryId = group.Key.PlexLibraryId,
                        MediaIds = group.Select(x => x.Id).ToList(),
                        Qualities = [],
                    })
                    .ToList(),
            }
        );
        SetupFamilyCommandExecutor();

        // Act
        var result = await TestHandlerExecuteAsync<List<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(movies.Count);
        result.Value.ShouldAllBe(x => x.MediaType == PlexMediaType.Movie);
    }

    private void SetupFamilyCommandExecutor()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetMovieDownloadPreviewCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (ICommand<Result<IEnumerable<DownloadPreview>>> command, CancellationToken ct) =>
                    new GetMovieDownloadPreviewCommandHandler(IDbContext).ExecuteAsync(
                        (GetMovieDownloadPreviewCommand)command,
                        ct
                    )
            );
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetTvShowDownloadPreviewCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (ICommand<Result<IEnumerable<DownloadPreview>>> command, CancellationToken ct) =>
                    new GetTvShowDownloadPreviewCommandHandler(IDbContext, Log).ExecuteAsync(
                        (GetTvShowDownloadPreviewCommand)command,
                        ct
                    )
            );
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetMusicDownloadPreviewCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (ICommand<Result<IEnumerable<DownloadPreview>>> command, CancellationToken ct) =>
                    new GetMusicDownloadPreviewCommandHandler(IDbContext).ExecuteAsync(
                        (GetMusicDownloadPreviewCommand)command,
                        ct
                    )
            );
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetOtherVideoDownloadPreviewCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (ICommand<Result<IEnumerable<DownloadPreview>>> command, CancellationToken ct) =>
                    new GetOtherVideoDownloadPreviewCommandHandler(IDbContext).ExecuteAsync(
                        (GetOtherVideoDownloadPreviewCommand)command,
                        ct
                    )
            );
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetPhotoDownloadPreviewCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (ICommand<Result<IEnumerable<DownloadPreview>>> command, CancellationToken ct) =>
                    new GetPhotoDownloadPreviewCommandHandler(Log, IDbContext).ExecuteAsync(
                        (GetPhotoDownloadPreviewCommand)command,
                        ct
                    )
            );
    }
}
