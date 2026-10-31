namespace Reaparr.Application.UnitTests;

public class SetupRadarrIntegrationCommandUnitTests : BaseCommandUnitTest<SetupRadarrIntegrationCommand>
{
    [Test]
    public async Task ShouldKeepRemoteIdsAndUnconfiguredState_WhenValidationFails()
    {
        // Arrange
        await SetupDatabase(55343, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        await IDbContext.RadarrIntegrations.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.ProvisioningState, IntegrationProvisioningState.Unconfigured),
            CancellationToken
        );
        var command = new SetupRadarrIntegrationCommand(integration.Id);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupRadarrDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Ok(
                    new SetupRadarrDownloadClientCommandResult
                    {
                        DownloadClientId = 41,
                        Resource = new RadarrDownloadContractDTO(),
                    }
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupRadarrIndexerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Ok(
                    new SetupRadarrIndexerCommandResult { IndexerId = 42, Resource = new RadarrIndexerContractDTO() }
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ValidateRadarrIntegrationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Reaparr resource validation failed"))
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendIntegrationSetupProgressAsync(It.IsAny<IntegrationSetupProgressDTO>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(5));

        // Act
        var result = await TestHandlerExecuteAsync<RadarrIntegration>(command);
        var updated = await IDbContext.RadarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        updated.ExternalDownloadClientId.ShouldBe(41);
        updated.ExternalIndexerId.ShouldBe(42);
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Unconfigured);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IProgressHubService>().Verify();
    }

    [Test]
    public async Task ShouldMarkConfiguredAndConnected_WhenScopedValidationSucceeds()
    {
        // Arrange
        await SetupDatabase(55344, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        await IDbContext.RadarrIntegrations.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.ProvisioningState, IntegrationProvisioningState.Unconfigured),
            CancellationToken
        );
        var command = new SetupRadarrIntegrationCommand(integration.Id);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupRadarrDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Ok(
                    new SetupRadarrDownloadClientCommandResult
                    {
                        DownloadClientId = 51,
                        Resource = new RadarrDownloadContractDTO(),
                    }
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupRadarrIndexerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Ok(
                    new SetupRadarrIndexerCommandResult { IndexerId = 52, Resource = new RadarrIndexerContractDTO() }
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ValidateRadarrIntegrationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendIntegrationSetupProgressAsync(It.IsAny<IntegrationSetupProgressDTO>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(6));

        // Act
        var result = await TestHandlerExecuteAsync<RadarrIntegration>(command);
        var updated = await IDbContext.RadarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        updated.ExternalDownloadClientId.ShouldBe(51);
        updated.ExternalIndexerId.ShouldBe(52);
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Configured);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Success);
        updated.LastConnectionTestHttpStatusCode.ShouldBe(StatusCodes.Status200OK);
        updated.LastConnectionTestErrorMessage.ShouldBeNull();
        updated.LastConnectionTestedAt.ShouldNotBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IProgressHubService>().Verify();
    }

    [Test]
    public async Task ShouldStopSetup_WhenDownloadClientSetupIsCancelled()
    {
        // Arrange
        await SetupDatabase(55345, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        await IDbContext.RadarrIntegrations.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.ProvisioningState, IntegrationProvisioningState.Unconfigured),
            CancellationToken
        );
        var command = new SetupRadarrIntegrationCommand(integration.Id);
        var cancelled = ResultExtensions
            .TaskIsCancelled(nameof(SetupRadarrDownloadClientCommand))
            .ToResult<SetupRadarrDownloadClientCommandResult>();

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupRadarrDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cancelled)
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendIntegrationSetupProgressAsync(It.IsAny<IntegrationSetupProgressDTO>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await TestHandlerExecuteAsync<RadarrIntegration>(command);
        var updated = await IDbContext.RadarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Unconfigured);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<SetupRadarrIndexerCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ValidateRadarrIntegrationCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IProgressHubService>().Verify();
    }

    [Test]
    public async Task ShouldStopSetup_WhenIndexerSetupFails()
    {
        // Arrange
        await SetupDatabase(55346, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        await IDbContext.RadarrIntegrations.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.ProvisioningState, IntegrationProvisioningState.Unconfigured),
            CancellationToken
        );
        var command = new SetupRadarrIntegrationCommand(integration.Id);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupRadarrDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Ok(
                    new SetupRadarrDownloadClientCommandResult
                    {
                        DownloadClientId = 61,
                        Resource = new RadarrDownloadContractDTO(),
                    }
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupRadarrIndexerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<SetupRadarrIndexerCommandResult>("Indexer setup failed"))
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendIntegrationSetupProgressAsync(It.IsAny<IntegrationSetupProgressDTO>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(3));

        // Act
        var result = await TestHandlerExecuteAsync<RadarrIntegration>(command);
        var updated = await IDbContext.RadarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        updated.ExternalDownloadClientId.ShouldBe(61);
        updated.ExternalIndexerId.ShouldBeNull();
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Unconfigured);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ValidateRadarrIntegrationCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IProgressHubService>().Verify();
    }
}
