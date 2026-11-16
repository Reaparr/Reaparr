using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Application.UnitTests;

public class DeleteSonarrIntegrationEndpointUnitTests
    : BaseEndpointUnitTest<DeleteSonarrIntegrationEndpoint, DeleteSonarrIntegrationRequest>
{
    [Test]
    public async Task ShouldDeleteLocalIntegration_WhenRemoteClientCreationFailsAndForceIsSet()
    {
        // Arrange
        await SetupDatabase(70402, config =>
        {
            config.SonarrIntegrationCount = 2;
        });
        var dbContext = IDbContext;
        var integrations = await dbContext.SonarrIntegrations.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var target = integrations[0];
        var retained = integrations[1];
        var remoteFailure = Result.Fail<HttpClient>("Sonarr is unavailable");

        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.CreateAsync(target.Id))
            .ReturnsAsync(remoteFailure)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(remoteFailure, TelemetryActivity.ExternalDownloadRemoval))
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.IntegrationDeleted(
                    It.Is<SonarrIntegration>(integration => integration.Id == target.Id),
                    It.Is<ResultBase>(result => ReferenceEquals(result, remoteFailure)),
                    localRemoved: true,
                    forced: true
                )
            )
            .Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(
            new DeleteSonarrIntegrationRequest { IntegrationId = target.Id, Force = true },
            extraServices: services => services.AddSingleton(Mock.Mock<ISonarrHttpClientFactory>().Object)
        );

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status204NoContent);
        var remainingIds = await dbContext
            .SonarrIntegrations.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        remainingIds.ShouldBe([retained.Id]);
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldDeleteLocalIntegrationAndCaptureCleanupFailure_WhenRemoteCleanupFailsAndForceIsSet()
    {
        // Arrange
        await SetupDatabase(70404, config =>
        {
            config.SonarrIntegrationCount = 2;
        });
        var dbContext = IDbContext;
        var integrations = await dbContext
            .SonarrIntegrations.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var target = integrations[0];
        var retained = integrations[1];
        target.ExternalIndexerId = 123;
        await dbContext.SaveChangesAsync(CancellationToken);
        (
            await dbContext
                .SonarrIntegrations.Where(x => x.Id == target.Id)
                .Select(x => x.ExternalIndexerId)
                .SingleAsync(CancellationToken)
        ).ShouldBe(123);

        var client = new HttpClient(new StatusCodeHandler(HttpStatusCode.InternalServerError, "Server Error"))
        {
            BaseAddress = new Uri("https://sonarr.example.com/"),
        };
        ResultBase? capturedCleanupResult = null;
        ResultBase? capturedDeletedResult = null;
        bool? capturedLocalRemoved = null;
        bool? capturedForced = null;

        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.CreateAsync(target.Id))
            .ReturnsAsync(Result.Ok(client))
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.ExternalDownloadRemoval))
            .Callback<ResultBase, TelemetryActivity>((result, _) => capturedCleanupResult = result)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.IntegrationDeleted(
                    It.Is<SonarrIntegration>(integration => integration.Id == target.Id),
                    It.IsAny<ResultBase>(),
                    true,
                    true
                )
            )
            .Callback<SonarrIntegration, ResultBase, bool, bool>(
                (_, result, localRemoved, forced) =>
                {
                    capturedDeletedResult = result;
                    capturedLocalRemoved = localRemoved;
                    capturedForced = forced;
                }
            )
            .Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(
            new DeleteSonarrIntegrationRequest { IntegrationId = target.Id, Force = true },
            extraServices: services => services.AddSingleton(Mock.Mock<ISonarrHttpClientFactory>().Object)
        );

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status204NoContent);
        var remainingIds = await dbContext
            .SonarrIntegrations.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        remainingIds.ShouldBe([retained.Id]);
        capturedCleanupResult.ShouldNotBeNull();
        capturedCleanupResult.IsFailed.ShouldBeTrue();
        capturedDeletedResult.ShouldNotBeNull();
        ReferenceEquals(capturedCleanupResult, capturedDeletedResult).ShouldBeTrue();
        capturedLocalRemoved.ShouldBe(true);
        capturedForced.ShouldBe(true);
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    private sealed class StatusCodeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _reasonPhrase;

        public StatusCodeHandler(HttpStatusCode statusCode, string reasonPhrase)
        {
            _statusCode = statusCode;
            _reasonPhrase = reasonPhrase;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(_statusCode) { RequestMessage = request, ReasonPhrase = _reasonPhrase }
            );
    }
}
