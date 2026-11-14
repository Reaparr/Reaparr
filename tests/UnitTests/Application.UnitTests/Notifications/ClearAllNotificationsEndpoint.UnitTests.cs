namespace Reaparr.Application.UnitTests;

public class ClearAllNotificationsEndpointUnitTests
    : BaseEndpointWithoutRequestUnitTest<ClearAllNotificationsEndpoint, CountResponseDTO>
{
    [Test]
    public async Task ShouldReportDeletedCount_WhenNotificationsAreCleared()
    {
        // Arrange
        await SetupDatabase(53103);
        var db = IDbContext;
        db.Notifications.AddRange(
            new Notification
            {
                Id = 9004,
                Level = NotificationLevel.Information,
                Message = "first",
                Hidden = false,
            },
            new Notification
            {
                Id = 9005,
                Level = NotificationLevel.Error,
                Message = "second",
                Hidden = true,
            }
        );
        await db.SaveChangesAsync(CancellationToken);
        Mock.Mock<ITelemetry>().Setup(x => x.NotificationsCleared(2)).Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync();

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<ResultDTO<CountResponseDTO>>(
                outcome.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response.Value.ShouldNotBeNull().Count.ShouldBe(2);
        (await db.Notifications.CountAsync(CancellationToken)).ShouldBe(0);
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldReportNoOp_WhenNoNotificationsExist()
    {
        // Arrange
        await SetupDatabase(53104);
        Mock.Mock<ITelemetry>().Setup(x => x.NotificationsCleared(0)).Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync();

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<ResultDTO<CountResponseDTO>>(
                outcome.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response.Value.ShouldNotBeNull().Count.ShouldBe(0);
        Mock.Mock<ITelemetry>().Verify();
    }
}
