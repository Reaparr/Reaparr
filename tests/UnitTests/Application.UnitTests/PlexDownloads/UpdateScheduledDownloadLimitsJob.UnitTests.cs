using Quartz;

namespace Reaparr.Application.UnitTests;

public class UpdateScheduledDownloadLimitsJobUnitTests : BaseUnitTest<UpdateScheduledDownloadLimitsJob>
{
    [Test]
    [Arguments("Asia/Kathmandu", "2026-10-04T18:30:00Z", "2026-10-05T00:15:00+05:45")]
    [Arguments("America/New_York", "2026-11-01T05:45:00Z", "2026-11-01T01:45:00-04:00")]
    [Arguments("America/New_York", "2026-11-01T06:45:00Z", "2026-11-01T01:45:00-05:00")]
    public async Task ShouldEvaluateTheConfiguredWallClock_WhenOffsetsOrRepeatedHoursDifferFromUtc(
        string timeZone,
        string utc,
        string expected
    )
    {
        // Arrange
        var settings = new UserSettings();
        settings.DateTimeSettings.TimeZone = timeZone;
        var expectedLocalTime = DateTimeOffset.Parse(expected);
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterInstance(new FixedTimeProvider(DateTimeOffset.Parse(utc))).As<TimeProvider>();
        });
        var context = new Mock<IJobExecutionContext>();
        context.SetupProperty(x => x.Result);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<UpdateScheduledDownloadLimitsCommand>(command =>
                        command.LocalTime.EqualsExact(expectedLocalTime)
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        ((BackgroundJobResult)context.Object.Result!).Status.ShouldBe(JobStatus.Completed);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
