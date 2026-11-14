namespace Reaparr.Application.UnitTests;

public class CheckConnectionStatusByIdCommandUnitTests : BaseCommandUnitTest<CheckConnectionStatusByIdCommand>
{
    [Test]
    public async Task ShouldUpdateExistingStatus_WhenConnectionAlreadyHasStatus()
    {
        // Arrange
        await SetupDatabase(
            57231,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexServerConnection = await dbContext.PlexServerConnections.SingleAsync(CancellationToken);
        var existingStatus = await dbContext.PlexServerStatuses.SingleAsync(CancellationToken);
        var statusCount = await dbContext.PlexServerStatuses.CountAsync(CancellationToken);

        Mock.SetupCommand(It.IsAny<GetServerStatusCommand>)
            .ReturnsAsync(
                Result.Ok(
                    new PlexServerStatus
                    {
                        IsSuccessful = false,
                        StatusCode = 503,
                        StatusMessage = "The Plex server could not be reached.",
                        LastChecked = DateTime.UtcNow.AddMinutes(1),
                        PlexServerId = plexServerConnection.PlexServerId,
                        PlexServerConnectionId = plexServerConnection.Id,
                    }
                )
            )
            .Verifiable(Times.Once);

        // Act
        var result = await TestHandlerExecuteAsync<PlexServerStatus>(
            new CheckConnectionStatusByIdCommand(plexServerConnection.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var statuses = await IDbContext.PlexServerStatuses.ToListAsync(CancellationToken);
        statuses.Count.ShouldBe(statusCount);

        var updatedStatus = statuses.Single();
        updatedStatus.Id.ShouldBe(existingStatus.Id);
        updatedStatus.IsSuccessful.ShouldBeFalse();
        updatedStatus.StatusCode.ShouldBe(503);
        updatedStatus.StatusMessage.ShouldBe("The Plex server could not be reached.");
        updatedStatus.PlexServerId.ShouldBe(plexServerConnection.PlexServerId);
        updatedStatus.PlexServerConnectionId.ShouldBe(plexServerConnection.Id);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(
                x => x.ConnectionCheckFinished(It.IsAny<int>(), It.IsAny<Result<PlexServerStatus>>()),
                Times.Never()
            );
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldInsertStatus_WhenConnectionDoesNotHaveStatus()
    {
        // Arrange
        await SetupDatabase(
            57232,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexServerConnection = await dbContext.PlexServerConnections.SingleAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        Mock.SetupCommand(It.IsAny<GetServerStatusCommand>)
            .ReturnsAsync(
                Result.Ok(
                    new PlexServerStatus
                    {
                        IsSuccessful = true,
                        StatusCode = 200,
                        StatusMessage = "The Plex server is online!",
                        LastChecked = DateTime.UtcNow,
                        PlexServerId = plexServerConnection.PlexServerId,
                        PlexServerConnectionId = plexServerConnection.Id,
                    }
                )
            )
            .Verifiable(Times.Once);

        // Act
        var result = await TestHandlerExecuteAsync<PlexServerStatus>(
            new CheckConnectionStatusByIdCommand(plexServerConnection.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var insertedStatus = await IDbContext.PlexServerStatuses.SingleAsync(CancellationToken);
        insertedStatus.IsSuccessful.ShouldBeTrue();
        insertedStatus.StatusCode.ShouldBe(200);
        insertedStatus.StatusMessage.ShouldBe("The Plex server is online!");
        insertedStatus.PlexServerId.ShouldBe(plexServerConnection.PlexServerId);
        insertedStatus.PlexServerConnectionId.ShouldBe(plexServerConnection.Id);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(
                x => x.ConnectionCheckFinished(It.IsAny<int>(), It.IsAny<Result<PlexServerStatus>>()),
                Times.Never()
            );
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
    }
}
