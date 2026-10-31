namespace Reaparr.Application.UnitTests;

public class SetupSonarrIntegrationCommandUnitTests : BaseCommandUnitTest<SetupSonarrIntegrationCommand>
{
    [Test]
    public async Task ShouldMarkConfigured_WhenScopedValidationSucceeds()
    {
        // Arrange
        await SetupDatabase(62643, config => config.SonarrIntegrationCount = 1);
        var integration = await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken);
        await IDbContext.SonarrIntegrations.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.ProvisioningState, IntegrationProvisioningState.Unconfigured),
            CancellationToken
        );
        var command = new SetupSonarrIntegrationCommand(integration.Id);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupSonarrDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Ok(
                    new SetupSonarrDownloadClientCommandResult
                    {
                        DownloadClientId = 51,
                        Resource = new SonarrDownloadContractDTO(),
                    }
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupSonarrIndexerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Ok(
                    new SetupSonarrIndexerCommandResult { IndexerId = 52, Resource = new SonarrIndexerContractDTO() }
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ValidateSonarrIntegrationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendIntegrationSetupProgressAsync(It.IsAny<IntegrationSetupProgressDTO>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(6));

        // Act
        var result = await TestHandlerExecuteAsync<SonarrIntegration>(command);
        var updated = await IDbContext.SonarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);

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
        await SetupDatabase(62644, config => config.SonarrIntegrationCount = 1);
        var integration = await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken);
        await IDbContext.SonarrIntegrations.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.ProvisioningState, IntegrationProvisioningState.Unconfigured),
            CancellationToken
        );
        var command = new SetupSonarrIntegrationCommand(integration.Id);
        var cancelled = ResultExtensions
            .TaskIsCancelled(nameof(SetupSonarrDownloadClientCommand))
            .ToResult<SetupSonarrDownloadClientCommandResult>();

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupSonarrDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cancelled)
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendIntegrationSetupProgressAsync(It.IsAny<IntegrationSetupProgressDTO>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<SonarrIntegration>(command);
        var updated = await IDbContext.SonarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Unconfigured);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<SetupSonarrIndexerCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ValidateSonarrIntegrationCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IProgressHubService>().Verify();
    }

    [Test]
    public async Task ShouldStopSetup_WhenIndexerSetupFails()
    {
        // Arrange
        await SetupDatabase(62645, config => config.SonarrIntegrationCount = 1);
        var integration = await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken);
        await IDbContext.SonarrIntegrations.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.ProvisioningState, IntegrationProvisioningState.Unconfigured),
            CancellationToken
        );
        var command = new SetupSonarrIntegrationCommand(integration.Id);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupSonarrDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Ok(
                    new SetupSonarrDownloadClientCommandResult
                    {
                        DownloadClientId = 61,
                        Resource = new SonarrDownloadContractDTO(),
                    }
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SetupSonarrIndexerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<SetupSonarrIndexerCommandResult>("Indexer setup failed"))
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendIntegrationSetupProgressAsync(It.IsAny<IntegrationSetupProgressDTO>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(3));

        // Act
        var result = await TestHandlerExecuteAsync<SonarrIntegration>(command);
        var updated = await IDbContext.SonarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        updated.ExternalDownloadClientId.ShouldBe(61);
        updated.ExternalIndexerId.ShouldBeNull();
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Unconfigured);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ValidateSonarrIntegrationCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IProgressHubService>().Verify();
    }
}
