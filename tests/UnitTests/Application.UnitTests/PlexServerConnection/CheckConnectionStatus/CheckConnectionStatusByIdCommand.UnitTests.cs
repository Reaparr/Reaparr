namespace Reaparr.Application.UnitTests;

public class CheckConnectionStatusByIdCommandUnitTests : BaseCommandUnitTest<CheckConnectionStatusByIdCommand>
{
    [Test]
    public async Task ShouldUpdateExistingStatus_WhenConnectionAlreadyHasStatus()
    {
        // Arrange
        await SetupDatabase(57231, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 1;
        });

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
        await SetupDatabase(57232, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 1;
        });

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

    [Test]
    public async Task ShouldReturnEntityNotFoundAndNotInsertStatus_WhenConnectionIsDeletedBeforePersistence()
    {
        // Arrange
        await SetupDatabase(57233, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 1;
        });

        var dbContext = IDbContext;
        var plexServerConnection = await dbContext.PlexServerConnections.SingleAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        var transactionContext = new Mock<IReaparrDbContext>(MockBehavior.Strict);
        transactionContext.SetupGet(x => x.PlexServerConnections).Returns(dbContext.PlexServerConnections);
        transactionContext.SetupGet(x => x.PlexServerStatuses).Returns(dbContext.PlexServerStatuses);
        transactionContext.Setup(x => x.Dispose()).Verifiable(Times.Once);
        transactionContext
            .Setup(x =>
                x.ExecuteTransactionAsync(
                    It.IsAny<Func<IReaparrDbContext, CancellationToken, Task<PlexServerStatus?>>>(
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (
                    Func<IReaparrDbContext, CancellationToken, Task<PlexServerStatus?>> operation,
                    CancellationToken token
                ) =>
                {
                    await dbContext.PlexServerConnections
                        .Where(x => x.Id == plexServerConnection.Id)
                        .ExecuteDeleteAsync(token);
                    return Result.Ok(await operation(transactionContext.Object, token));
                }
            )
            .Verifiable(Times.Once);
        var factory = new Mock<IReaparrDbContextFactory>(MockBehavior.Strict);
        factory
            .Setup(x => x.CreateAsync())
            .ReturnsAsync(transactionContext.Object)
            .Verifiable(Times.Once);
        SetupDependencies(builder =>
            builder.RegisterInstance(factory.Object).As<IReaparrDbContextFactory>().SingleInstance()
        );

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
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        factory.Verify();
        (await IDbContext.PlexServerStatuses.AnyAsync(x => x.PlexServerConnectionId == plexServerConnection.Id, CancellationToken))
            .ShouldBeFalse();
        transactionContext.Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

}
