using Quartz;

namespace Reaparr.Application.UnitTests;

[NotInParallel]
public class LibrarySyncJobCompletionUnitTests : BaseUnitTest<LibrarySyncJob>
{
    [Test]
    [Arguments(LibrarySyncJobStatus.Completed, false)]
    [Arguments(LibrarySyncJobStatus.Failed, false)]
    [Arguments(LibrarySyncJobStatus.Cancelled, false)]
    [Arguments(LibrarySyncJobStatus.Completed, true)]
    [Arguments(LibrarySyncJobStatus.Cancelled, true)]
    [Arguments(LibrarySyncJobStatus.Queued, false)]
    public async Task ShouldContinueQueueOnceAndPreserveComparisonEligibility_WhenSyncEnds(
        LibrarySyncJobStatus expectedStatus,
        bool notificationFails
    )
    {
        // Arrange
        var offline = expectedStatus == LibrarySyncJobStatus.Queued;
        var libraries = await SetupQueueAsync(offline);
        var current = libraries[0];
        var next = libraries[1];
        using var jobCancellation = new CancellationTokenSource();
        if (expectedStatus == LibrarySyncJobStatus.Cancelled)
            jobCancellation.Cancel();
        var context = SetupContext(current, jobCancellation.Token);
        SetupRefresh(current, expectedStatus, notificationFails);
        var checker = Mock.Create<CheckQueuedPlexLibraryToSyncCommandHandler>();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(),
                It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)))
            .Returns((CheckQueuedPlexLibraryToSyncCommand command, CancellationToken token) => checker.ExecuteAsync(command, token))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        var nextKey = LibrarySyncJob.GetJobKey(next.PlexServerId, next.Id);
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(nextKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IScheduler>()
            .Setup(x => x.ScheduleJob(
                It.Is<IJobDetail>(job => job.Key.Equals(nextKey) &&
                    job.JobDataMap.GetPayload<LibrarySyncJobPayload>() == new LibrarySyncJobPayload(next.PlexServerId, next.Id)),
                It.Is<ITrigger>(trigger => trigger.JobKey.Equals(nextKey)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero))
            .Verifiable(offline ? Times.Never() : Times.Once());

        // Act
        await Sut.Execute(context.Object);
        await Mock.Create<LibrarySyncJobListener>().JobWasExecuted(context.Object, null, CancellationToken);

        // Assert
        await AssertQueueAsync(current, next, expectedStatus);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.Is<ScheduleAffectedLibraryComparisonJobsCommand>(command => command.PlexLibraryId == current.Id),
            It.IsAny<CancellationToken>()), expectedStatus == LibrarySyncJobStatus.Completed ? Times.Once() : Times.Never());
        Mock.Mock<IScheduler>().Verify(x => x.ScheduleJob(
            It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()), offline ? Times.Never() : Times.Once());
    }

    private async Task<List<PlexLibrary>> SetupQueueAsync(bool offline)
    {
        SetupDependencies(builder => builder.RegisterType<MediaOverviewRebuildCoordinator>()
            .As<IMediaOverviewRebuildCoordinator>().SingleInstance());
        await SetupDatabase(55109, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 2;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Select(x => x.PlexServerId).Distinct().ShouldBe([libraries[0].PlexServerId]);
        dbContext.LibrarySyncJobQueues.AddRange(libraries.Select((library, index) => new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = index + 1,
            Status = index == 0 ? LibrarySyncJobStatus.Processing : LibrarySyncJobStatus.Queued,
            CreatedAt = new DateTime(2026, 10, 22, 11, 0, 0, DateTimeKind.Utc),
        }));
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSuccessful, !offline), CancellationToken);
        (await dbContext.LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId).Select(x => x.Status)
            .ToListAsync(CancellationToken)).ShouldBe([LibrarySyncJobStatus.Processing, LibrarySyncJobStatus.Queued]);
        return libraries;
    }

    private void SetupRefresh(PlexLibrary library, LibrarySyncJobStatus expectedStatus, bool notificationFails)
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<RefreshLibraryMediaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatus == LibrarySyncJobStatus.Failed
                ? Result.Fail<PlexLibrary>("Unauthorized").Add401UnauthorizedError()
                : Result.Ok(library))
            .Verifiable(expectedStatus is LibrarySyncJobStatus.Queued or LibrarySyncJobStatus.Cancelled ? Times.Never() : Times.Once());
        var notification = Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.Is<List<RefreshDataType>>(types => types.SequenceEqual(
                new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus }))));
        if (notificationFails)
            notification.ThrowsAsync(new InvalidOperationException("Notification failed")).Verifiable(Times.Once());
        else
            notification.Returns(Task.CompletedTask).Verifiable(Times.Once());
    }

    private async Task AssertQueueAsync(PlexLibrary current, PlexLibrary next, LibrarySyncJobStatus expectedStatus)
    {
        var rows = await IDbContext.LibrarySyncJobQueues.AsNoTracking().OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var offline = expectedStatus == LibrarySyncJobStatus.Queued;
        rows.Select(x => new { x.PlexLibraryId, x.Status, x.IsServerOffline }).ShouldBe([
            new { PlexLibraryId = current.Id, Status = expectedStatus, IsServerOffline = offline },
            new { PlexLibraryId = next.Id, Status = offline ? LibrarySyncJobStatus.Queued : LibrarySyncJobStatus.Processing, IsServerOffline = offline },
        ]);
        rows[0].CompletedAt.HasValue.ShouldBe(!offline);
        rows[1].StartedAt.HasValue.ShouldBe(!offline);
        rows[1].CompletedAt.ShouldBeNull();
    }

    private static Mock<IJobExecutionContext> SetupContext(PlexLibrary library, CancellationToken cancellationToken)
    {
        var payload = new LibrarySyncJobPayload(library.PlexServerId, library.Id).ToJobDataMap();
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(payload);
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(payload);
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        context.SetupProperty(x => x.Result);
        return context;
    }
}
