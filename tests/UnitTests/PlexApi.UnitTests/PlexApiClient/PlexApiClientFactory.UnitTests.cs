using Reaparr.Data.Contracts;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class PlexApiClientFactoryUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldPropagateTokenFailureMetadata_WhenTokenIsUnavailable()
    {
        // Arrange
        await SetupDatabase(
            9211,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 0;
            }
        );
        var serverId = IDbContext.PlexServers.Single().Id;
        var expectedTokenResult = await IDbContext.GetPlexServerTokenAsync(serverId, CancellationToken);
        var dbContextFactory = new Mock<IReaparrDbContextFactory>(MockBehavior.Strict);
        dbContextFactory.Setup(x => x.CreateAsync()).ReturnsAsync(IDbContext).Verifiable(Times.Once());
        var clientFactory = new PlexApiClientFactory(_ => Moq.Mock.Of<IPlexApiClient>(), dbContextFactory.Object);

        // Act
        var result = await clientFactory.CreateClientAsync(serverId);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.ValueOrDefault.ShouldBeNull();
        result.Errors.Count.ShouldBe(expectedTokenResult.Errors.Count);
        result.Errors.Select(x => x.Message).ShouldBe(expectedTokenResult.Errors.Select(x => x.Message));
        result.HasPlex401UnauthorizedError().ShouldBeTrue();
        result
            .GetStatusCodeReason()!
            .Metadata[ResultExtensions.StatusCodeName]
            .ShouldBe(expectedTokenResult.GetStatusCodeReason()!.Metadata[ResultExtensions.StatusCodeName]);
        dbContextFactory.Verify();
    }

    [Test]
    public async Task ShouldPropagateConnectionFailure_WhenServerHasNoConnections()
    {
        // Arrange
        await SetupDatabase(
            9212,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexServerConnectionPerServerCount = 0;
            }
        );
        var serverId = IDbContext.PlexServers.Single().Id;
        var expectedConnectionResult = await IDbContext.ChoosePlexServerConnection(serverId, CancellationToken);
        var dbContextFactory = new Mock<IReaparrDbContextFactory>(MockBehavior.Strict);
        dbContextFactory.Setup(x => x.CreateAsync()).ReturnsAsync(IDbContext).Verifiable(Times.Once());
        var clientFactory = new PlexApiClientFactory(_ => Moq.Mock.Of<IPlexApiClient>(), dbContextFactory.Object);

        // Act
        var result = await clientFactory.CreateClientAsync(serverId);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.ValueOrDefault.ShouldBeNull();
        result.Errors.Count.ShouldBe(expectedConnectionResult.Errors.Count);
        result.Errors.Select(x => x.Message).ShouldBe(expectedConnectionResult.Errors.Select(x => x.Message));
        dbContextFactory.Verify();
    }

    [Test]
    public async Task ShouldCreateConfiguredClient_WhenTokenAndConnectionAreAvailable()
    {
        // Arrange
        await SetupDatabase(
            9213,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexServerConnectionPerServerCount = 1;
            }
        );
        var serverId = IDbContext.PlexServers.Single().Id;
        var expectedConnectionResult = await IDbContext.ChoosePlexServerConnection(serverId, CancellationToken);
        PlexApiClientOptions? capturedOptions = null;
        var dbContextFactory = new Mock<IReaparrDbContextFactory>(MockBehavior.Strict);
        dbContextFactory.Setup(x => x.CreateAsync()).ReturnsAsync(IDbContext).Verifiable(Times.Once());
        var clientFactory = new PlexApiClientFactory(
            options =>
            {
                capturedOptions = options;
                return Moq.Mock.Of<IPlexApiClient>();
            },
            dbContextFactory.Object
        );

        // Act
        var result = await clientFactory.CreateClientAsync(serverId, timeout: 27, retryCount: 4);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull();
        capturedOptions.ShouldNotBeNull();
        capturedOptions!.ConnectionUrl.ShouldBe(expectedConnectionResult.Value.Url);
        capturedOptions.Timeout.ShouldBe(27);
        capturedOptions.RetryCount.ShouldBe(4);
        dbContextFactory.Verify();
    }
}
