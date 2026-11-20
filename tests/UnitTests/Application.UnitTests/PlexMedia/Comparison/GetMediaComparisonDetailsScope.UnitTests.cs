using Quartz;

namespace Reaparr.Application.UnitTests;

public class GetMediaComparisonDetailsScopeUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(PlexMediaType.Movie, false, 0)]
    [Arguments(PlexMediaType.Movie, true, 0)]
    [Arguments(PlexMediaType.TvShow, false, 0)]
    [Arguments(PlexMediaType.TvShow, true, 0)]
    [Arguments(PlexMediaType.Movie, false, 1)]
    [Arguments(PlexMediaType.Movie, true, 1)]
    [Arguments(PlexMediaType.TvShow, false, 1)]
    [Arguments(PlexMediaType.TvShow, true, 1)]
    [Arguments(PlexMediaType.Movie, false, 2)]
    [Arguments(PlexMediaType.Movie, true, 2)]
    [Arguments(PlexMediaType.TvShow, false, 2)]
    [Arguments(PlexMediaType.TvShow, true, 2)]
    public async Task ShouldReturnNotCompared_WhenNoCurrentScopeExists(
        PlexMediaType mediaType,
        bool isOwned,
        int outdatedSide
    )
    {
        // Arrange
        var (remote, owned) = await SetupLibrariesAsync(mediaType);
        var library = isOwned ? owned : remote;
        var opposite = isOwned ? remote : owned;
        if (outdatedSide != 0)
        {
            await AddCurrentScopeAsync(remote, owned, mediaType);
            var outdatedId = outdatedSide == 1 ? library.Id : opposite.Id;
            await IDbContext
                .PlexLibraries.Where(x => x.Id == outdatedId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        }
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        scheduler
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), CancellationToken))
            .ReturnsAsync(new PagedResult<FireInstance>([], false));
        scheduler
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), CancellationToken))
            .ReturnsAsync(
                new PagedResult<JobHeader>(
                    [
                        new JobHeader(
                            PlexLibraryComparisonJob.GetJobKey(90001, 90002),
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
        var result = await GetDetailsAsync(mediaType, library.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldBe(PlexMediaComparisonState.NotCompared);
        result.Value.Rows.ShouldBeEmpty();
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
