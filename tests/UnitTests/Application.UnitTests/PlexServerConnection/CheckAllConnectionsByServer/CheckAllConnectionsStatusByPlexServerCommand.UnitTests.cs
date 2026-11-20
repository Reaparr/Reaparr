namespace Reaparr.Application.UnitTests;

public class CheckAllConnectionsStatusByPlexServerCommandUnitTests
    : BaseUnitTest<CheckAllConnectionsStatusByPlexServerHandler>
{
    [Test]
    public async Task ShouldReturnServerNotEnabled_WhenPlexServerIsDisabled()
    {
        // Arrange
        await SetupDatabase(5231, config =>
        {
            config.PlexServerCount = 1;
        });
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
        await SetupDatabase(4321, config =>
        {
            config.PlexServerCount = 1;
        });

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
    public async Task ShouldAwaitOnlineStatusChangePublication_WhenConnectionsBecomeHealthyWithoutProductTelemetry()
    {
        // Arrange
        await SetupDatabase(54231, config =>
        {
            config.PlexServerCount = 1;
        });

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

        var publishStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ServerOnlineStatusChangedNotification>(),
                    Mode.WaitForAll,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => publishStarted.TrySetResult(true))
            .Returns(publishCompleted.Task)
            .Verifiable(Times.Once);
        // Act
        var request = new CheckAllConnectionsStatusByPlexServerCommand(1);
        var execution = Sut.ExecuteAsync(request, CancellationToken);
        var firstCompletedTask = await Task.WhenAny(publishStarted.Task, execution);
        firstCompletedTask.ShouldBe(publishStarted.Task);
        execution.IsCompleted.ShouldBeFalse();
        publishCompleted.SetResult(true);
        var result = await execution;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Select(x => x.PlexServerConnectionId).Order().ShouldBe(connections.Select(x => x.Id).Order());
        result.Value.ShouldAllBe(x => x.PlexServerId == 1 && x.IsSuccessful);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<CheckConnectionStatusByIdCommand>(command =>
                            command.Timeout == 10
                            && connections.Select(connection => connection.Id).Contains(command.PlexServerConnectionId)
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(connections.Count)
            );
        Mock.Mock<ITelemetry>()
            .Verify(
                x => x.ConnectionCheckFinished(It.IsAny<int>(), It.IsAny<Result<List<PlexServerStatus>>>()),
                Times.Never()
            );
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x =>
                    x.PublishAsync(
                        It.IsAny<ServerOnlineStatusChangedNotification>(),
                        Mode.WaitForAll,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldNotPublishServerOnlineStatusChangedNotification_WhenOnlineStatusHasNotChanged()
    {
        // Arrange
        await SetupDatabase(523187, config =>
        {
            config.PlexServerCount = 1;
        });

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
                x.PublishAsync(
                    It.IsAny<ServerOnlineStatusChangedNotification>(),
                    Mode.WaitForAll,
                    It.IsAny<CancellationToken>()
                )
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
                x =>
                    x.PublishAsync(
                        It.IsAny<ServerOnlineStatusChangedNotification>(),
                        Mode.WaitForAll,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldNotPublishOnlineNotification_WhenAllConnectionChecksReturnUnsuccessfulStatuses()
    {
        // Arrange
        await SetupDatabase(523188, config =>
        {
            config.PlexServerCount = 1;
        });

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
                x => x.ConnectionCheckFinished(It.IsAny<int>(), It.IsAny<Result<List<PlexServerStatus>>>()),
                Times.Never()
            );
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x =>
                    x.PublishAsync(
                        It.IsAny<ServerOnlineStatusChangedNotification>(),
                        Mode.WaitForAll,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPublishOfflineNotification_WhenServerTransitionsFromOnlineToOffline()
    {
        // Arrange
        await SetupDatabase(523189, config =>
        {
            config.PlexServerCount = 1;
        });

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
                x.PublishAsync(
                    It.IsAny<ServerOnlineStatusChangedNotification>(),
                    Mode.WaitForAll,
                    It.IsAny<CancellationToken>()
                )
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
                        Mode.WaitForAll,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldIgnoreRemovedConnection404_WhenSurvivingConnectionIsHealthy()
    {
        // Arrange
        await SetupDatabase(523190, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 2;
        });

        var dbContext = IDbContext;
        var connections = dbContext.PlexServerConnections.OrderBy(x => x.Id).ToList();
        var removedId = connections[0].Id;
        var survivorId = connections[1].Id;
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .Returns(
                async (CheckConnectionStatusByIdCommand request, CancellationToken _) =>
                {
                    if (request.PlexServerConnectionId == removedId)
                    {
                        await dbContext.PlexServerConnections.Where(x => x.Id == removedId).ExecuteDeleteAsync(_);
                        return ResultExtensions
                            .EntityNotFound(nameof(PlexServerConnection), removedId)
                            .ToResult<PlexServerStatus>();
                    }

                    return Result.Ok(FakeData.GetPlexServerStatus(new Seed(523190), true, 1, survivorId).Generate());
                }
            )
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await Sut.ExecuteAsync(new CheckAllConnectionsStatusByPlexServerCommand(1), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Select(x => x.PlexServerConnectionId).ShouldBe([survivorId]);
        result.Value.Single().IsSuccessful.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldUsePersistedStatusForReplacementConnection_WhenSnapshottedConnectionIsRemoved()
    {
        // Arrange
        await SetupDatabase(523191, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 1;
        });

        var dbContext = IDbContext;
        var original = dbContext.PlexServerConnections.Single();
        var replacement = FakeData.GetPlexServerConnections(new Seed(523192), plexServerId: 1).Generate();

        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .Returns(
                async (CheckConnectionStatusByIdCommand request, CancellationToken token) =>
                {
                    await dbContext.PlexServerConnections.Where(x => x.Id == original.Id).ExecuteDeleteAsync(token);
                    dbContext.PlexServerConnections.Add(replacement);
                    await dbContext.SaveChangesAsync(token);
                    dbContext.PlexServerStatuses.Add(
                        FakeData.GetPlexServerStatus(new Seed(523193), true, 1, replacement.Id).Generate()
                    );
                    await dbContext.SaveChangesAsync(token);
                    return ResultExtensions
                        .EntityNotFound(nameof(PlexServerConnection), request.PlexServerConnectionId)
                        .ToResult<PlexServerStatus>();
                }
            )
            .Verifiable(Times.Once);

        // Act
        var result = await Sut.ExecuteAsync(new CheckAllConnectionsStatusByPlexServerCommand(1), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Select(x => x.PlexServerConnectionId).ShouldBe([replacement.Id]);
        result.Value.Single().IsSuccessful.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldPropagateFailureFromCurrentConnection_WhenAnotherSnapshottedConnectionWasRemoved()
    {
        // Arrange
        await SetupDatabase(523194, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 2;
        });

        var dbContext = IDbContext;
        var connections = dbContext.PlexServerConnections.OrderBy(x => x.Id).ToList();
        var removedId = connections[0].Id;
        var failure = Result.Fail<PlexServerStatus>("current connection failed");
        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .Returns(
                async (CheckConnectionStatusByIdCommand request, CancellationToken token) =>
                {
                    if (request.PlexServerConnectionId == removedId)
                    {
                        await dbContext.PlexServerConnections.Where(x => x.Id == removedId).ExecuteDeleteAsync(token);
                        return ResultExtensions
                            .EntityNotFound(nameof(PlexServerConnection), removedId)
                            .ToResult<PlexServerStatus>();
                    }

                    return failure;
                }
            )
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await Sut.ExecuteAsync(new CheckAllConnectionsStatusByPlexServerCommand(1), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().Message.ShouldBe("current connection failed");
    }

    [Test]
    public async Task ShouldPropagateCancellation_WhenConnectionCheckIsCancelled()
    {
        // Arrange
        await SetupDatabase(523195, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 1;
        });
        using var cancellation = new CancellationTokenSource();
        var observedToken = CancellationToken.None;
        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .ReturnsAsync(
                (CheckConnectionStatusByIdCommand _, CancellationToken token) =>
                {
                    observedToken = token;
                    return ResultExtensions
                        .TaskIsCancelled(nameof(CheckConnectionStatusByIdCommand))
                        .ToResult<PlexServerStatus>();
                }
            )
            .Verifiable(Times.AtLeastOnce);

        // Act
        var result = await Sut.ExecuteAsync(new CheckAllConnectionsStatusByPlexServerCommand(1), cancellation.Token);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        observedToken.ShouldBe(cancellation.Token);
    }

    [Test]
    public async Task ShouldNotPublishOnlineStatus_WhenAllSnapshottedConnectionsWereRemoved()
    {
        // Arrange
        await SetupDatabase(523196, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 2;
        });

        var dbContext = IDbContext;
        var connectionIds = dbContext.PlexServerConnections.Select(x => x.Id).ToList();
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .Returns(
                async (CheckConnectionStatusByIdCommand request, CancellationToken _) =>
                {
                    await dbContext
                        .PlexServerConnections.Where(x => x.Id == request.PlexServerConnectionId)
                        .ExecuteDeleteAsync(_);
                    return ResultExtensions
                        .EntityNotFound(nameof(PlexServerConnection), request.PlexServerConnectionId)
                        .ToResult<PlexServerStatus>();
                }
            )
            .Verifiable(Times.Exactly(connectionIds.Count));

        // Act
        var result = await Sut.ExecuteAsync(new CheckAllConnectionsStatusByPlexServerCommand(1), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBeEmpty();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x =>
                    x.PublishAsync(
                        It.IsAny<ServerOnlineStatusChangedNotification>(),
                        Mode.WaitForAll,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
    }

    [Test]
    public async Task ShouldPropagateNotFound_WhenConnectionStillExistsAfterReconciliation()
    {
        // Arrange
        await SetupDatabase(523197, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 1;
        });

        var connectionId = IDbContext.PlexServerConnections.Single().Id;
        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .ReturnsAsync(
                ResultExtensions.EntityNotFound(nameof(PlexServerConnection), connectionId).ToResult<PlexServerStatus>()
            )
            .Verifiable(Times.Once);

        // Act
        var result = await Sut.ExecuteAsync(new CheckAllConnectionsStatusByPlexServerCommand(1), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Has404NotFoundError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldExcludeSuccessfulProbe_WhenConnectionIsRemovedBeforeReconciliation()
    {
        // Arrange
        await SetupDatabase(523198, config =>
        {
            config.PlexServerCount = 1;
            config.PlexServerConnectionPerServerCount = 2;
        });

        var dbContext = IDbContext;
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            p => p.SetProperty(x => x.IsSuccessful, false),
            CancellationToken
        );

        var connections = dbContext.PlexServerConnections.OrderBy(x => x.Id).ToList();

        var removedConnectionIds = connections.Select(x => x.Id).ToHashSet();
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .Returns(
                async (CheckConnectionStatusByIdCommand request, CancellationToken _) =>
                {
                    await dbContext
                        .PlexServerConnections.Where(x => x.Id == request.PlexServerConnectionId)
                        .ExecuteDeleteAsync(_);

                    if (request.PlexServerConnectionId == connections[0].Id)
                    {
                        return ResultExtensions
                            .EntityNotFound(nameof(PlexServerConnection), request.PlexServerConnectionId)
                            .ToResult<PlexServerStatus>();
                    }

                    return Result.Ok(
                        FakeData
                            .GetPlexServerStatus(
                                new Seed(523198),
                                isSuccessful: true,
                                plexServerId: 1,
                                plexServerConnectionId: request.PlexServerConnectionId
                            )
                            .Generate()
                    );
                }
            )
            .Verifiable(Times.Exactly(removedConnectionIds.Count));

        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ServerOnlineStatusChangedNotification>(),
                    Mode.WaitForAll,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never);

        // Act
        var result = await Sut.ExecuteAsync(new CheckAllConnectionsStatusByPlexServerCommand(1), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Once);
        Mock.Mock<IEventPublisher>()
            .Verify(
                x =>
                    x.PublishAsync(
                        It.IsAny<ServerOnlineStatusChangedNotification>(),
                        Mode.WaitForAll,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
    }
}
