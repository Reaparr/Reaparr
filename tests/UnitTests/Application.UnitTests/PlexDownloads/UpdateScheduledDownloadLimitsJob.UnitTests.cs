using Quartz;

namespace Reaparr.Application.UnitTests;

public class UpdateScheduledDownloadLimitsJobUnitTests : BaseUnitTest<UpdateScheduledDownloadLimitsJob>
{
    [Test]
    [Arguments(false, JobStatus.Completed)]
    [Arguments(true, JobStatus.Failed)]
    public async Task ShouldRecordRecalculationOutcome_WhenCommandCompletes(bool failed, JobStatus expectedStatus)
    {
        // Arrange
        var commandResult = failed ? Result.Fail("Schedule recalculation failed.") : Result.Ok();
        var context = new Mock<IJobExecutionContext>();
        context.SetupProperty(x => x.Result);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<UpdateScheduledDownloadLimitsCommand>(command => command.LocalTime == null),
                    CancellationToken
                )
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        ((BackgroundJobResult)context.Object.Result!).Status.ShouldBe(expectedStatus);
        Mock.Mock<ICommandExecutor>().Verify();
        context.Verify();
    }

    [Test]
    [Arguments("2026-10-04T18:00:00Z", "2026-10-04T18:15:00Z")]
    [Arguments("2026-10-04T18:15:01Z", "2026-10-04T18:45:00Z")]
    public void ShouldFireAtBothLocalHalfHourBoundaries_WhenTheTimezoneHasAQuarterHourOffset(
        string after,
        string expected
    )
    {
        // Arrange
        var trigger = UpdateScheduledDownloadLimitsJob
            .CreateTrigger("Asia/Kathmandu")
            .GetTriggerBuilder()
            .StartAt(DateTimeOffset.UnixEpoch)
            .Build();

        // Act
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.Parse(after));

        // Assert
        nextFireTime.ShouldBe(DateTimeOffset.Parse(expected));
    }
}
