using System.Net;

namespace Reaparr.Application.UnitTests;

public class NotifyArrAppsOnStartupCommandUnitTests : BaseCommandUnitTest<NotifyArrAppsOnStartupCommand>
{
    [Test]
    public async Task ShouldTestConfiguredIntegrationsConcurrentlyAndSkipTestAllWhenConnectionsFail()
    {
        // Arrange
        await SetupDatabase(90001, config =>
        {
            config.RadarrIntegrationCount = 2;
            config.SonarrIntegrationCount = 2;
        });
        var dbContext = IDbContext;
        var radarrIds = await dbContext
            .RadarrIntegrations.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        var sonarrIds = await dbContext
            .SonarrIntegrations.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        await dbContext
            .RadarrIntegrations.Where(x => x.Id == radarrIds[1])
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.ProvisioningState, IntegrationProvisioningState.Unconfigured),
                CancellationToken
            );
        await dbContext
            .SonarrIntegrations.Where(x => x.Id == sonarrIds[1])
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.ProvisioningState, IntegrationProvisioningState.Unconfigured),
                CancellationToken
            );
        var release = new TaskCompletionSource<Result<TestConnectionResult>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedCount = 0;

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<TestConnectionToRadarrCommand>(command => command.IntegrationId == radarrIds[0]),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                if (Interlocked.Increment(ref startedCount) == 2)
                    bothStarted.SetResult();
                return release.Task;
            })
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<TestConnectionToSonarrCommand>(command => command.IntegrationId == sonarrIds[0]),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                if (Interlocked.Increment(ref startedCount) == 2)
                    bothStarted.SetResult();
                return release.Task;
            })
            .Verifiable(Times.Once());
        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.CreateAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Result.Fail<HttpClient>("Should not be called"))
            .Verifiable(Times.Never());
        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.CreateAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Result.Fail<HttpClient>("Should not be called"))
            .Verifiable(Times.Never());

        // Act
        var execution = TestHandlerExecuteAsync(new NotifyArrAppsOnStartupCommand());
        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var connectionFailure = Result.Ok(
            new TestConnectionResult
            {
                Status = TestConnectionStatus.ConnectionFailed,
                ErrorMessage = "Connection failed.",
                TestedAt = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
            }
        );
        release.SetResult(connectionFailure);
        var result = await execution;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        startedCount.ShouldBe(2);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldCallAppSpecificTestAllAfterSuccessfulConnectionTests()
    {
        // Arrange
        await SetupDatabase(90002, config =>
        {
            config.RadarrIntegrationCount = 1;
            config.SonarrIntegrationCount = 1;
        });
        var dbContext = IDbContext;
        var radarrId = await dbContext.RadarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var sonarrId = await dbContext.SonarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var connectionSuccess = Result.Ok(
            new TestConnectionResult
            {
                Status = TestConnectionStatus.Success,
                HttpStatusCode = StatusCodes.Status200OK,
                TestedAt = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
            }
        );
        var radarrHandler = new StatusCodeHandler(HttpStatusCode.BadRequest);
        var sonarrHandler = new StatusCodeHandler(HttpStatusCode.OK);
        var radarrClient = new HttpClient(radarrHandler) { BaseAddress = new Uri("http://radarr.test") };
        var sonarrClient = new HttpClient(sonarrHandler) { BaseAddress = new Uri("http://sonarr.test") };

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<TestConnectionToRadarrCommand>(command => command.IntegrationId == radarrId),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(connectionSuccess)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<TestConnectionToSonarrCommand>(command => command.IntegrationId == sonarrId),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(connectionSuccess)
            .Verifiable(Times.Once());
        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.CreateAsync(radarrId))
            .ReturnsAsync(Result.Ok(radarrClient))
            .Verifiable(Times.Once());
        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.CreateAsync(sonarrId))
            .ReturnsAsync(Result.Ok(sonarrClient))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new NotifyArrAppsOnStartupCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        radarrHandler.RequestCount.ShouldBe(1);
        radarrHandler.LastRequestMethod.ShouldBe(HttpMethod.Post);
        radarrHandler.LastRequestPath.ShouldBe("/api/v3/downloadclient/testall");
        sonarrHandler.RequestCount.ShouldBe(1);
        sonarrHandler.LastRequestMethod.ShouldBe(HttpMethod.Post);
        sonarrHandler.LastRequestPath.ShouldBe("/api/v3/downloadclient/testall");
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldPropagateCancellationWithoutCallingTestAll()
    {
        // Arrange
        await SetupDatabase(90003, config => config.RadarrIntegrationCount = 1);
        var integrationId = await IDbContext.RadarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var cancelled = ResultExtensions
            .TaskIsCancelled(nameof(TestConnectionToRadarrCommand))
            .ToResult<TestConnectionResult>();

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<TestConnectionToRadarrCommand>(command => command.IntegrationId == integrationId),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(cancelled)
            .Verifiable(Times.Once());
        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.CreateAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Result.Fail<HttpClient>("Should not be called"))
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync(new NotifyArrAppsOnStartupCommand());

        // Assert
        result.IsCancelled.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
    }

    private sealed class StatusCodeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public HttpMethod? LastRequestMethod { get; private set; }
        public string? LastRequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestCount++;
            LastRequestMethod = request.Method;
            LastRequestPath = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(statusCode) { RequestMessage = request });
        }
    }
}
