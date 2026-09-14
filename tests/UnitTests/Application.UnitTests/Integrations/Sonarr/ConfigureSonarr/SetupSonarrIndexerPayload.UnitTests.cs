namespace Reaparr.Application.UnitTests;

public class SetupSonarrIndexerPayloadUnitTests : BaseUnitTest<SetupSonarrIndexerCommandHandler>
{
    [Test]
    public async Task ShouldForceCreateWithPersistedTorznabApiKey()
    {
        // Arrange
        await SetupDatabase(62644, config => config.SonarrIntegrationCount = 1);
        var integration = await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken);
        var expectedApiKey = integration.TorznabApiKey;
        SonarrApiCreateIndexerCommand? capturedCommand = null;
        var sut = Mock.Create<SetupSonarrIndexerCommandHandler>(
            new TypedParameter(typeof(INetworkSettings), NetworkSettingsModule.Create())
        );

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SonarrApiGetIndexersCommand>(command => command.IntegrationId == integration.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok(new List<IndexerResourceDTO>()))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SonarrApiCreateIndexerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (SonarrApiCreateIndexerCommand command, CancellationToken _) =>
                {
                    capturedCommand = command;
                    return Result.Ok(new SonarrIndexerContractDTO { Id = 52 });
                }
            )
            .Verifiable(Times.Once());

        // Act
        var result = await sut.ExecuteAsync(
            new SetupSonarrIndexerCommand { IntegrationId = integration.Id, DownloadClientId = 51 },
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedCommand.ShouldNotBeNull();
        capturedCommand.ForceSave.ShouldBeTrue();
        capturedCommand.Resource.Fields!.ShouldContain(x => x.Name == "apiKey" && Equals(x.Value, expectedApiKey));
        capturedCommand.Resource.Fields!.ShouldNotContain(x => x.Name == "apikey");
        capturedCommand
            .Resource.Fields!.Single(x => x.Name == "categories")
            .Value.ShouldBe(IntegrationDefinitions.SupportedTorznabCategories.Select(x => (int)x.Id).ToList());
        result.Value.Resource.ShouldBeSameAs(capturedCommand.Resource);
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
