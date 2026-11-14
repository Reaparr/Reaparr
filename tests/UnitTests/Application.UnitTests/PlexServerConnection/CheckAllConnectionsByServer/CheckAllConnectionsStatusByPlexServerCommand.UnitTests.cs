namespace Reaparr.Application.UnitTests;

public class CheckAllConnectionsStatusByPlexServerCommandUnitTests
    : BaseUnitTest<CheckAllConnectionsStatusByPlexServerHandler>
{
    [Test]
    public async Task ShouldReturnServerNotEnabled_WhenPlexServerIsDisabled()
    {
        // Arrange
        await SetupDatabase(
            5231,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );
        var plexServer = IDbContext.PlexServers.First();
        await IDbContext.PlexServers.ExecuteUpdateAsync(p => p.SetProperty(x => x.IsEnabled, false), CancellationToken);

        // Act
        var request = new CheckAllConnectionsStatusByPlexServerCommand(plexServer.Id);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnEntityNotFound_WhenPlexServerDoesNotExist()
    {
        // Arrange
        await SetupDatabase(5231);
        var request = new CheckAllConnectionsStatusByPlexServerCommand(999);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnNoConnectionsFound_WhenPlexServerHasNoConnections()
    {
        // Arrange
        await SetupDatabase(
            4321,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexServer = dbContext.PlexServers.First();
        await dbContext.PlexServerConnections.ExecuteDeleteAsync(CancellationToken);

        var request = new CheckAllConnectionsStatusByPlexServerCommand(plexServer.Id);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldPublishOnlineStatusChange_WhenConnectionsBecomeHealthyWithoutProductTelemetry()
    {
        // Arrange
        await SetupDatabase(
            54231,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );

        // Set server to offline
        var dbContext = IDbContext;
        await dbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        var connections = dbContext.PlexServerConnections.Where(x => x.PlexServerId == 1).ToList();
        foreach (var connection in connections)
        {
            dbContext.PlexServerStatuses.Add(
                FakeData
                    .GetPlexServerStatus(
                        new Seed(connection.Id),
                        isSuccessful: false,
                        plexServerId: 1,
                        plexServerConnectionId: connection.Id
                    )
                    .Generate()
            );
        }

        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<INotificationHubService>()
            .Setup(m => m.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .ReturnsAsync(
                (CheckConnectionStatusByIdCommand req, CancellationToken _) =>
                    Result.Ok(
                        FakeData
                            .GetPlexServerStatus(
                                new Seed(23),
                                isSuccessful: true,
                                plexServerId: 1,
                                plexServerConnectionId: req.PlexServerConnectionId
                            )
                            .Generate()
                    )
            )
            .Verifiable(Times.AtLeastOnce);

        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(It.IsAny<ServerOnlineStatusChangedNotification>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Act
        var request = new CheckAllConnectionsStatusByPlexServerCommand(1);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Select(x => x.PlexServerConnectionId).Order()
            .ShouldBe(connections.Select(x => x.Id).Order());
        result.Value.ShouldAllBe(x => x.PlexServerId == 1 && x.IsSuccessful);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<CheckConnectionStatusByIdCommand>(command =>
                            command.Timeout == 10
                            && connections.Select(connection => connection.Id).Contains(command.PlexServerConnectionId)),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(connections.Count)
            );
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.ConnectionCheckFinished(
                        It.IsAny<int>(),
                        It.IsAny<Result<List<PlexServerStatus>>>()
                    ),
                Times.Never()
            );
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<ServerOnlineStatusChangedNotification>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldNotPublishServerOnlineStatusChangedNotification_WhenOnlineStatusHasNotChanged()
    {
        // Arrange
        await SetupDatabase(
            523187,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );

        // Set server to online
        var dbContext = IDbContext;
        await dbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        var connections = dbContext.PlexServerConnections.Where(x => x.PlexServerId == 1).ToList();
        foreach (var connection in connections)
        {
            dbContext.PlexServerStatuses.Add(
                FakeData
                    .GetPlexServerStatus(
                        new Seed(connection.Id),
                        isSuccessful: true,
                        plexServerId: 1,
                        plexServerConnectionId: connection.Id
                    )
                    .Generate()
            );
        }

        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<INotificationHubService>()
            .Setup(m => m.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .ReturnsAsync(
                (CheckConnectionStatusByIdCommand req, CancellationToken _) =>
                    Result.Ok(
                        FakeData
                            .GetPlexServerStatus(
                                new Seed(23),
                                isSuccessful: true,
                                plexServerId: 1,
                                plexServerConnectionId: req.PlexServerConnectionId
                            )
                            .Generate()
                    )
            )
            .Verifiable(Times.AtLeastOnce);

        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(It.IsAny<ServerOnlineStatusChangedNotification>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never);

        // Act
        var request = new CheckAllConnectionsStatusByPlexServerCommand(1);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<ServerOnlineStatusChangedNotification>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldNotPublishOnlineNotification_WhenAllConnectionChecksReturnUnsuccessfulStatuses()
    {
        // Arrange
        await SetupDatabase(
            523188,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );

        var dbContext = IDbContext;
        await dbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        var connections = dbContext.PlexServerConnections.Where(x => x.PlexServerId == 1).ToList();

        Mock.Mock<INotificationHubService>()
            .Setup(m => m.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .ReturnsAsync(
                (CheckConnectionStatusByIdCommand req, CancellationToken _) =>
                    Result.Ok(
                        FakeData
                            .GetPlexServerStatus(
                                new Seed(44),
                                isSuccessful: false,
                                plexServerId: 1,
                                plexServerConnectionId: req.PlexServerConnectionId
                            )
                            .Generate()
                    )
            )
            .Verifiable(Times.Exactly(connections.Count));


        // Act
        var request = new CheckAllConnectionsStatusByPlexServerCommand(1);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsFailed.ShouldBeTrue();
        result.ValueOrDefault.ShouldNotBeNull();
        result.ValueOrDefault!.Select(status => status.IsSuccessful).ShouldBe(connections.Select(_ => false));
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.ConnectionCheckFinished(
                        It.IsAny<int>(),
                        It.IsAny<Result<List<PlexServerStatus>>>()
                    ),
                Times.Never()
            );
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<ServerOnlineStatusChangedNotification>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPublishOfflineNotification_WhenServerTransitionsFromOnlineToOffline()
    {
        // Arrange
        await SetupDatabase(
            523189,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );

        var dbContext = IDbContext;
        await dbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        var connections = dbContext.PlexServerConnections.Where(x => x.PlexServerId == 1).ToList();
        foreach (var connection in connections)
        {
            dbContext.PlexServerStatuses.Add(
                FakeData
                    .GetPlexServerStatus(
                        new Seed(connection.Id),
                        isSuccessful: true,
                        plexServerId: 1,
                        plexServerConnectionId: connection.Id
                    )
                    .Generate()
            );
        }

        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<INotificationHubService>()
            .Setup(m => m.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .ReturnsAsync(
                (CheckConnectionStatusByIdCommand req, CancellationToken _) =>
                    Result.Ok(
                        FakeData
                            .GetPlexServerStatus(
                                new Seed(45),
                                isSuccessful: false,
                                plexServerId: 1,
                                plexServerConnectionId: req.PlexServerConnectionId
                            )
                            .Generate()
                    )
            )
            .Verifiable(Times.Exactly(connections.Count));

        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(It.IsAny<ServerOnlineStatusChangedNotification>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Act
        var request = new CheckAllConnectionsStatusByPlexServerCommand(1);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x =>
                    x.PublishAsync(
                        It.Is<ServerOnlineStatusChangedNotification>(notification =>
                            notification.PlexServerId == 1 && !notification.IsOnline
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }
}
