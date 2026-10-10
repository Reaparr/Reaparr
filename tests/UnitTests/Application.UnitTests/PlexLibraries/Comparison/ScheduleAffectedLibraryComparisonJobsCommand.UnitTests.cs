using Quartz;
using Quartz.Impl.Matchers;

namespace Reaparr.Application.UnitTests;

public class ScheduleAffectedLibraryComparisonJobsCommandUnitTests
    : BaseCommandUnitTest<ScheduleAffectedLibraryComparisonJobsCommand>
{
    [Test]
    [Arguments(PlexMediaType.Movie, false)]
    [Arguments(PlexMediaType.TvShow, false)]
    [Arguments(PlexMediaType.MusicArtist, false)]
    [Arguments(PlexMediaType.Movie, true)]
    [Arguments(PlexMediaType.TvShow, true)]
    [Arguments(PlexMediaType.MusicArtist, true)]
    public async Task ShouldScheduleOnlySameTypeOppositeRolePairs_WhenEitherSideChanges(PlexMediaType type, bool owned)
    {
        // Arrange
        await SetupDatabase(91112, config =>
        {
            config.PlexServerCount = 5;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(5);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, type), CancellationToken);
        await dbContext.PlexLibraries.Where(x => x.Id == libraries[3].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, PlexMediaType.OtherVideos), CancellationToken);
        await dbContext.PlexServers.ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, !owned), CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, owned);
        await SetOwnedOverrideAsync(libraries[2].PlexServerId, owned);
        var expectedPayloads = new[] { libraries[1], libraries[4] }.Select(target => owned
            ? new PlexLibraryComparisonJobPayload(libraries[0].Id, target.Id)
            : new PlexLibraryComparisonJobPayload(target.Id, libraries[0].Id)).ToList();
        var expectedKeys = expectedPayloads.Select(payload =>
            PlexLibraryComparisonJob.GetJobKey(payload.OwnedPlexLibraryId, payload.RemotePlexLibraryId)).ToList();
        IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>? scheduled = null;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.LibraryComparisonJob)), CancellationToken))
            .ReturnsAsync([]).Verifiable(Times.Once());
        scheduler.Setup(x => x.CheckExists(It.Is<JobKey>(key => expectedKeys.Contains(key)), CancellationToken))
            .ReturnsAsync(false).Verifiable(Times.Exactly(2));
        scheduler.Setup(x => x.ScheduleJobs(It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(), false, CancellationToken))
            .Callback<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>, bool, CancellationToken>((jobs, _, _) => scheduled = jobs)
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scheduled.ShouldNotBeNull();
        scheduled.Keys.Select(x => x.Key).ShouldBe(expectedKeys);
        scheduled.Keys.Select(x => x.JobDataMap.GetPayload<PlexLibraryComparisonJobPayload>()).ShouldBe(expectedPayloads);
        scheduled.Values.ShouldAllBe(x => x.Count == 1);
        scheduled.Values.SelectMany(x => x).Select(x => x.JobKey).ShouldBe(expectedKeys);
        scheduler.Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.MusicArtist)]
    public async Task ShouldNotScheduleDuplicatePair_WhenJobAlreadyExists(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(91113, config => { config.PlexServerCount = 2; config.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, type), CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        var key = PlexLibraryComparisonJob.GetJobKey(libraries[1].Id, libraries[0].Id);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.LibraryComparisonJob)), CancellationToken))
            .ReturnsAsync([key]).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scheduler.Verify();
        scheduler.Verify(x => x.ScheduleJobs(It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(), false, It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.OtherVideos)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    public async Task ShouldNotSchedule_WhenNoEligiblePairExists(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(91114, config => { config.PlexServerCount = 2; config.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, type), CancellationToken);
        await dbContext.PlexServers.ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, false), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IScheduler>().Verify(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>().Verify(x => x.ScheduleJobs(It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(), false, It.IsAny<CancellationToken>()), Times.Never());
    }
}
