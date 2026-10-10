using Quartz;
using Quartz.Impl.Matchers;

namespace Reaparr.Application.UnitTests;

public class InvalidateLibraryComparisonJobsCommandUnitTests
    : BaseUnitTest<InvalidateLibraryComparisonJobsCommandHandler>
{
    [Test]
    public async Task ShouldDeleteExactComparisonJobKeysForAffectedLibrary()
    {
        // Arrange
        await SetupDatabase(91114);

        var affectedLibraryId = 12;
        var ownedJobKey = PlexLibraryComparisonJob.GetJobKey(affectedLibraryId, 34);
        var remoteJobKey = PlexLibraryComparisonJob.GetJobKey(56, affectedLibraryId);
        var scheduler = Mock.Mock<IScheduler>();
        var ownedJobDetail = new Mock<IJobDetail>();
        ownedJobDetail
            .SetupGet(x => x.JobDataMap)
            .Returns(new PlexLibraryComparisonJobPayload(affectedLibraryId, 34).ToJobDataMap());
        var remoteJobDetail = new Mock<IJobDetail>();
        remoteJobDetail
            .SetupGet(x => x.JobDataMap)
            .Returns(new PlexLibraryComparisonJobPayload(56, affectedLibraryId).ToJobDataMap());
        var unaffectedJobKey = PlexLibraryComparisonJob.GetJobKey(7, 8);
        var unaffectedJobDetail = new Mock<IJobDetail>();
        unaffectedJobDetail
            .SetupGet(x => x.JobDataMap)
            .Returns(new PlexLibraryComparisonJobPayload(7, 8).ToJobDataMap());

        scheduler
            .Setup(x =>
                x.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.LibraryComparisonJob)), CancellationToken)
            )
            .ReturnsAsync([ownedJobKey, remoteJobKey, unaffectedJobKey]);
        scheduler.Setup(x => x.GetJobDetail(ownedJobKey, CancellationToken)).ReturnsAsync(ownedJobDetail.Object);
        scheduler.Setup(x => x.GetJobDetail(remoteJobKey, CancellationToken)).ReturnsAsync(remoteJobDetail.Object);
        scheduler
            .Setup(x => x.GetJobDetail(unaffectedJobKey, CancellationToken))
            .ReturnsAsync(unaffectedJobDetail.Object);
        scheduler.Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken)).ReturnsAsync([]);
        scheduler
            .Setup(x =>
                x.DeleteJobs(
                    It.Is<IReadOnlyCollection<JobKey>>(keys =>
                        keys.Count == 2 && keys.Any(y => y == ownedJobKey) && keys.Any(y => y == remoteJobKey)
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(true);

        // Act
        var result = await Sut.ExecuteAsync(
            new InvalidateLibraryComparisonJobsCommand([affectedLibraryId]),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        scheduler.VerifyAll();
    }
    [Test]
    public async Task ShouldCancelAndAwaitRunningComparisonBeforeDeletingResults()
    {
        // Arrange
        await SetupDatabase(
            91115,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.Movie);
        var dbContext = IDbContext;
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var jobKey = PlexLibraryComparisonJob.GetJobKey(ownedLibrary.Id, remoteLibrary.Id);
        var scheduler = Mock.Mock<IScheduler>();
        var isExecuting = true;
        var interrupted = false;
        ConfigureAffectedJob(
            scheduler,
            jobKey,
            new PlexLibraryComparisonJobPayload(ownedLibrary.Id, remoteLibrary.Id),
            () => isExecuting,
            () =>
            {
                interrupted = true;
                isExecuting = false;
            },
            interruptSucceeds: true
        );
        scheduler
            .Setup(x => x.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), CancellationToken))
            .Callback<IReadOnlyCollection<JobKey>, CancellationToken>((_, _) => interrupted.ShouldBeTrue())
            .ReturnsAsync(true);

        // Act
        var result = await Sut.ExecuteAsync(
            new InvalidateLibraryComparisonJobsCommand([ownedLibrary.Id]),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        interrupted.ShouldBeTrue();
        (await dbContext.PlexMovieComparisons.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldPreserveComparisonResults_WhenRunningJobCannotBeCancelled()
    {
        // Arrange
        await SetupDatabase(
            91116,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary, PlexMediaType.Movie);
        var dbContext = IDbContext;
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var jobKey = PlexLibraryComparisonJob.GetJobKey(ownedLibrary.Id, remoteLibrary.Id);
        var scheduler = Mock.Mock<IScheduler>();
        ConfigureAffectedJob(
            scheduler,
            jobKey,
            new PlexLibraryComparisonJobPayload(ownedLibrary.Id, remoteLibrary.Id),
            () => true,
            static () => { },
            interruptSucceeds: false
        );

        // Act
        var result = await Sut.ExecuteAsync(
            new InvalidateLibraryComparisonJobsCommand([ownedLibrary.Id]),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        (await dbContext.PlexMovieComparisons.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(1);
        scheduler.Verify(
            x => x.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    private static void ConfigureAffectedJob(
        Mock<IScheduler> scheduler,
        JobKey jobKey,
        PlexLibraryComparisonJobPayload payload,
        Func<bool> isExecuting,
        Action onInterrupt,
        bool interruptSucceeds
    )
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(payload.ToJobDataMap());
        var executionContext = new Mock<IJobExecutionContext>();
        var executingJobDetail = new Mock<IJobDetail>();
        executingJobDetail.SetupGet(x => x.Key).Returns(jobKey);
        executionContext.SetupGet(x => x.JobDetail).Returns(executingJobDetail.Object);

        scheduler
            .Setup(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([jobKey]);
        scheduler.Setup(x => x.GetJobDetail(jobKey, It.IsAny<CancellationToken>())).ReturnsAsync(jobDetail.Object);
        scheduler
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
                isExecuting()
                    ? new List<IJobExecutionContext> { executionContext.Object }
                    : new List<IJobExecutionContext>()
            );
        scheduler.Setup(x => x.CheckExists(jobKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var trigger = new Mock<ITrigger>();
        trigger.SetupGet(x => x.Key).Returns(new TriggerKey("comparison-trigger", "test"));
        scheduler
            .Setup(x => x.GetTriggersOfJob(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([trigger.Object]);
        scheduler
            .Setup(x => x.GetTriggerState(trigger.Object.Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TriggerState.Normal);
        scheduler.Setup(x => x.DeleteJob(jobKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        scheduler
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .Callback<JobKey, CancellationToken>((_, _) => onInterrupt())
            .ReturnsAsync(interruptSucceeds);
    }
}
