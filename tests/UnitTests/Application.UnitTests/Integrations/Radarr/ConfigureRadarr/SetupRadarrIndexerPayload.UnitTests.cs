using Reaparr.PublicAPI.Contracts;

namespace Reaparr.Application.UnitTests;

public class SetupRadarrIndexerPayloadUnitTests : BaseUnitTest<SetupRadarrIndexerCommandHandler>
{
    [Test]
    public async Task ShouldForceCreateWithPersistedTorznabApiKey()
    {
        // Arrange
        await SetupDatabase(55344, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var expectedApiKey = integration.TorznabApiKey;
        RadarrApiCreateIndexerCommand? capturedCommand = null;
        var sut = Mock.Create<SetupRadarrIndexerCommandHandler>(
            new TypedParameter(typeof(INetworkSettings), NetworkSettingsModule.Create())
        );

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RadarrApiGetIndexersCommand>(command => command.IntegrationId == integration.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok(new List<RadarrIndexerResourceDTO>()))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<RadarrApiCreateIndexerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (RadarrApiCreateIndexerCommand command, CancellationToken _) =>
                {
                    capturedCommand = command;
                    return Result.Ok(new RadarrIndexerResourceDTO { Id = 42 });
                }
            )
            .Verifiable(Times.Once());

        // Act
        var result = await sut.ExecuteAsync(
            new SetupRadarrIndexerCommand { IntegrationId = integration.Id, DownloadClientId = 41 },
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
