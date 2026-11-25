using Quartz;

namespace Reaparr.Application.UnitTests;

public class GetMediaComparisonDetailsScopeUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(PlexMediaType.Movie, false, true)]
    [Arguments(PlexMediaType.Movie, true, true)]
    [Arguments(PlexMediaType.TvShow, false, true)]
    [Arguments(PlexMediaType.TvShow, true, true)]
    [Arguments(PlexMediaType.Movie, false, false)]
    [Arguments(PlexMediaType.Movie, true, false)]
    [Arguments(PlexMediaType.TvShow, false, false)]
    [Arguments(PlexMediaType.TvShow, true, false)]
    public async Task ShouldReturnStoredUpgradesEvenWhenEitherLibraryIsOutdated(
        PlexMediaType mediaType,
        bool isOwned,
        bool sourceIsOutdated
    )
    {
        // Arrange
        var (remote, owned) = await SetupLibrariesAsync(mediaType);
        var library = isOwned ? owned : remote;
        var dbContext = IDbContext;
        await AddCurrentScopeAsync(remote, owned, mediaType);
        int expectedUpgradeId;
        if (mediaType == PlexMediaType.Movie)
        {
            var remoteMovie = await GetLibraryMovieAsync(remote.Id);
            var ownedMovie = await GetLibraryMovieAsync(owned.Id);
            expectedUpgradeId = remoteMovie.Id;
            dbContext.PlexMovieComparisons.Add(
                CreateMovieComparison(
                    remote.Id,
                    owned.Id,
                    remoteMovie.Id,
                    ownedMovie.Id,
                    PlexMediaComparisonHitState.HigherQuality,
                    VideoQuality.SD
                )
            );
        }
        else
        {
            var remoteShow = await GetLibraryTvShowAsync(remote.Id);
            var ownedShow = await GetLibraryTvShowAsync(owned.Id);
            var remoteEpisode = await dbContext.PlexTvShowEpisodes.SingleAsync(
                x => x.PlexLibraryId == remote.Id,
                CancellationToken
            );
            var ownedEpisode = await dbContext.PlexTvShowEpisodes.SingleAsync(
                x => x.PlexLibraryId == owned.Id,
                CancellationToken
            );
            expectedUpgradeId = remoteEpisode.Id;
            dbContext.PlexTvShowComparisons.Add(
                CreateTvShowComparison(remote.Id, owned.Id, remoteShow.Id, ownedShow.Id)
            );
            dbContext.PlexEpisodeComparisons.Add(
                CreateEpisodeComparison(
                    remote.Id,
                    owned.Id,
                    remoteEpisode.Id,
                    ownedEpisode.Id,
                    PlexMediaComparisonHitState.HigherQuality,
                    VideoQuality.SD
                )
            );
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var outdatedId = sourceIsOutdated == isOwned ? owned.Id : remote.Id;
        var updated = await dbContext
            .PlexLibraries.Where(x => x.Id == outdatedId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        updated.ShouldBe(1);
        var scheduler = Mock.Mock<IScheduler>();

        // Act
        var result = await GetDetailsAsync(mediaType, library.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Type.ShouldBe(mediaType);

        result.Value.State.ShouldBe(PlexMediaComparisonState.HigherQuality);
        var row = result.Value.Rows.Single();
        if (mediaType == PlexMediaType.TvShow)
        {
            row.Type.ShouldBe(PlexMediaType.Season);
            row = row.Children.Single();
        }
        row.PlexMediaId.ShouldBe(expectedUpgradeId);
        row.Type.ShouldBe(mediaType == PlexMediaType.Movie ? PlexMediaType.Movie : PlexMediaType.Episode);
        row.State.ShouldBe(PlexMediaComparisonState.HigherQuality);
        row.RemoteQuality.ShouldBe(VideoQuality.FullHD);
        row.OwnedQuality.ShouldBe(VideoQuality.SD);
        row.PlexLibraryId.ShouldBe(remote.Id);
        row.PlexServerId.ShouldBe(remote.PlexServerId);
        row.Children.ShouldBeEmpty();
        scheduler.Verify(
            x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        scheduler.Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.Movie, false)]
    [Arguments(PlexMediaType.Movie, true)]
    [Arguments(PlexMediaType.TvShow, false)]
    [Arguments(PlexMediaType.TvShow, true)]
    public async Task ShouldReturnNotCompared_WhenNoScopeOrJobExists(PlexMediaType mediaType, bool isOwned)
    {
        // Arrange
        var (remote, owned) = await SetupLibrariesAsync(mediaType);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        scheduler
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), CancellationToken))
            .ReturnsAsync(new PagedResult<FireInstance>([], false))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), CancellationToken))
            .ReturnsAsync(new PagedResult<JobHeader>([], false))
            .Verifiable(Times.Once());

        // Act
        var result = await GetDetailsAsync(mediaType, isOwned ? owned.Id : remote.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Type.ShouldBe(mediaType);
        result.Value.State.ShouldBe(PlexMediaComparisonState.NotCompared);
        result.Value.Rows.ShouldBeEmpty();
        scheduler.Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Movie, false, false)]
    [Arguments(PlexMediaType.Movie, true, false)]
    [Arguments(PlexMediaType.TvShow, false, false)]
    [Arguments(PlexMediaType.TvShow, true, false)]
    [Arguments(PlexMediaType.Movie, false, true)]
    [Arguments(PlexMediaType.Movie, true, true)]
    [Arguments(PlexMediaType.TvShow, false, true)]
    [Arguments(PlexMediaType.TvShow, true, true)]
    public async Task ShouldReturnPending_WhenComparisonIsQueuedOrRunning(
        PlexMediaType mediaType,
        bool isOwned,
        bool isRunning
    )
    {
        // Arrange
        var (remote, owned) = await SetupLibrariesAsync(mediaType);
        var key = PlexLibraryComparisonJob.GetJobKey(owned.Id, remote.Id);
        var execution = new Mock<IJobExecutionContext>();
        execution
            .SetupGet(x => x.JobDetail)
            .Returns(JobBuilder.Create<PlexLibraryComparisonJob>().WithIdentity(key).Build());
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        scheduler
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), CancellationToken))
            .ReturnsAsync(
                isRunning
                    ? new PagedResult<FireInstance>(
                        [
                            new FireInstance(
                                "fire-comparison",
                                new TriggerKey(key.Name, key.Group),
                                key,
                                "test-scheduler",
                                FireInstanceState.Executing,
                                DateTimeOffset.UnixEpoch,
                                DateTimeOffset.UnixEpoch,
                                null
                            ),
                        ],
                        false
                    )
                    : new PagedResult<FireInstance>([], false)
            );
        scheduler
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), CancellationToken))
            .ReturnsAsync(
                isRunning
                    ? new PagedResult<JobHeader>([], false)
                    : new PagedResult<JobHeader>(
                        [
                            new JobHeader(
                                key,
                                null,
                                typeof(PlexLibraryComparisonJob).FullName!,
                                false,
                                true,
                                false,
                                true
                            ),
                        ],
                        false
                    )
            );

        // Act
        var result = await GetDetailsAsync(mediaType, isOwned ? owned.Id : remote.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldBe(PlexMediaComparisonState.Pending);
        result.Value.Rows.ShouldBeEmpty();
    }

    private async Task<(PlexLibrary Remote, PlexLibrary Owned)> SetupLibrariesAsync(PlexMediaType mediaType)
    {
        await SetupDatabase(63580, config =>
        {
            config.PlexServerCount = 2;
            config.PlexAccountCount = 1;
            config.PlexMovieLibraryCount = mediaType == PlexMediaType.Movie ? 1 : 0;
            config.PlexTvShowLibraryCount = mediaType == PlexMediaType.TvShow ? 1 : 0;
            config.MovieCount = mediaType == PlexMediaType.Movie ? 1 : 0;
            config.TvShowCount = mediaType == PlexMediaType.TvShow ? 1 : 0;
            config.TvShowSeasonCount = mediaType == PlexMediaType.TvShow ? 1 : 0;
            config.TvShowEpisodeCount = mediaType == PlexMediaType.TvShow ? 1 : 0;
        });
        var libraries = await IDbContext
            .PlexLibraries.Where(x => x.Type == mediaType)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        return (libraries[0], libraries[1]);
    }

    private async Task<Result<PlexMediaComparisonDetailsDTO>> GetDetailsAsync(PlexMediaType mediaType, int libraryId)
    {
        if (mediaType == PlexMediaType.Movie)
        {
            var movie = await GetLibraryMovieAsync(libraryId);
            return await Mock.Create<GetMovieMediaComparisonDetailsCommandHandler>()
                .ExecuteAsync(new GetMovieMediaComparisonDetailsCommand(movie.Id), CancellationToken);
        }

        var show = await GetLibraryTvShowAsync(libraryId);
        return await Mock.Create<GetTvShowMediaComparisonDetailsCommandHandler>()
            .ExecuteAsync(new GetTvShowMediaComparisonDetailsCommand(show.Id), CancellationToken);
    }
}
