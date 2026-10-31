using Quartz;

namespace Reaparr.Application.UnitTests;

public class QuartzBatchDeletionUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldSucceed_WhenFalseBatchResultLeavesAllRequestedJobsAbsent()
    {
        // Arrange
        var keys = new[] { new JobKey("first", "comparison"), new JobKey("second", "comparison") };
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.DeleteJobs(It.Is<IReadOnlyCollection<JobKey>>(value => value.SequenceEqual(keys)), CancellationToken)
            )
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        foreach (var key in keys)
            scheduler.Setup(x => x.CheckExists(key, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.DeleteBatchJobs(keys, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldFail_WhenARequestedJobStillExistsAfterFalseBatchResult()
    {
        // Arrange
        var absentKey = new JobKey("absent", "comparison");
        var remainingKey = new JobKey("remaining", "comparison");
        var keys = new[] { absentKey, remainingKey };
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.DeleteJobs(It.Is<IReadOnlyCollection<JobKey>>(value => value.SequenceEqual(keys)), CancellationToken)
            )
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        scheduler.Setup(x => x.CheckExists(absentKey, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler
            .Setup(x => x.CheckExists(remainingKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.DeleteBatchJobs(keys, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldSucceedWithoutExistenceChecks_WhenBatchDeletionSucceeds()
    {
        // Arrange
        var key = new JobKey("deleted", "comparison");
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.DeleteJobs(
                    It.Is<IReadOnlyCollection<JobKey>>(value => value.Count == 1 && value.Contains(key)),
                    CancellationToken
                )
            )
            .ReturnsAsync(true)
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.DeleteBatchJobs([key], CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scheduler.Verify();
        scheduler.Verify(x => x.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldSucceedWithoutSchedulerCalls_WhenNoKeysAreRequested()
    {
        // Arrange
        var scheduler = Mock.Mock<IScheduler>();

        // Act
        var result = await scheduler.Object.DeleteBatchJobs([], CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scheduler.Verify(
            x => x.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        scheduler.Verify(x => x.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnSchedulerException_WhenDeletionOrAbsenceCheckThrows(bool checkingAbsence)
    {
        // Arrange
        var key = new JobKey("requested", "comparison");
        var failure = new InvalidOperationException("Scheduler unavailable");
        var scheduler = Mock.Mock<IScheduler>();
        var deletion = scheduler.Setup(x =>
            x.DeleteJobs(
                It.Is<IReadOnlyCollection<JobKey>>(value => value.Count == 1 && value.Contains(key)),
                CancellationToken
            )
        );
        if (checkingAbsence)
        {
            deletion.ReturnsAsync(false).Verifiable(Times.Once());
            scheduler.Setup(x => x.CheckExists(key, CancellationToken)).ThrowsAsync(failure).Verifiable(Times.Once());
        }
        else
            deletion.ThrowsAsync(failure).Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.DeleteBatchJobs([key], CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(failure);
        scheduler.Verify();
        if (!checkingAbsence)
            scheduler.Verify(x => x.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
