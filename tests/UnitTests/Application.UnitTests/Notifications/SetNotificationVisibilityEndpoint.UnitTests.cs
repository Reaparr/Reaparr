namespace Reaparr.Application.UnitTests;

public class SetNotificationVisibilityEndpointUnitTests
    : BaseEndpointUnitTest<SetNotificationVisibilityEndpoint, SetNotificationVisibilityEndpointRequest, BaseResultDTO>
{
    [Test]
    public async Task ShouldPersistVisibilityAndReportAffectedRows_WhenNotificationVisibilityChanges()
    {
        // Arrange
        await SetupDatabase(53101);
        var db = IDbContext;
        db.Notifications.AddRange(
            new Notification
            {
                Id = 9001,
                Level = NotificationLevel.Information,
                Message = "target",
                Hidden = false,
            },
            new Notification
            {
                Id = 9002,
                Level = NotificationLevel.Warning,
                Message = "control",
                Hidden = false,
            }
        );
        await db.SaveChangesAsync(CancellationToken);
        Mock.Mock<ITelemetry>()
            .Setup(x => x.NotificationVisibilityProcessed(9001, true, 1))
            .Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(new SetNotificationVisibilityEndpointRequest { Id = 9001 });

        // Assert
        outcome.IsValid.ShouldBeTrue();
        var response = outcome.Response.ShouldNotBeNull();
        (response.IsSuccess, response.Errors.Count).ShouldBe((true, 0));
        var after = await db.Notifications.OrderBy(x => x.Id).Select(x => new { x.Id, x.Hidden }).ToListAsync(CancellationToken);
        after.ShouldBe([
            new { Id = 9001, Hidden = true },
            new { Id = 9002, Hidden = false },
        ]);
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldExecuteUpdateAndReportAffectedRows_WhenNotificationAlreadyHasRequestedVisibility()
    {
        // Arrange
        await SetupDatabase(53102);
        var db = IDbContext;
        db.Notifications.Add(
            new Notification
            {
                Id = 9003,
                Level = NotificationLevel.Information,
                Message = "already hidden",
                Hidden = true,
            }
        );
        await db.SaveChangesAsync(CancellationToken);
        Mock.Mock<ITelemetry>()
            .Setup(x => x.NotificationVisibilityProcessed(9003, true, 1))
            .Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(new SetNotificationVisibilityEndpointRequest { Id = 9003 });

        // Assert
        outcome.IsValid.ShouldBeTrue();
        var response = outcome.Response.ShouldNotBeNull();
        (response.IsSuccess, response.Errors.Count).ShouldBe((true, 0));
        (await db.Notifications.SingleAsync(x => x.Id == 9003, CancellationToken)).Hidden.ShouldBeTrue();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldReturnNotFoundAndReportZeroAffectedRows_WhenNotificationDoesNotExist()
    {
        // Arrange
        await SetupDatabase(53105);
        Mock.Mock<ITelemetry>()
            .Setup(x => x.NotificationVisibilityProcessed(9006, true, 0))
            .Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(new SetNotificationVisibilityEndpointRequest { Id = 9006 });

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        var response = outcome.Response.ShouldNotBeNull();
        (response.IsSuccess, response.Errors.Count).ShouldBe((false, 1));
        Mock.Mock<ITelemetry>().Verify();
    }
}
