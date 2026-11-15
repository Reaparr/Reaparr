using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PostHog;
using Reaparr.Identity.Contracts;

namespace Reaparr.Application.UnitTests;

public class RefreshPlexAccountAccessEndpointUnitTests
    : BaseEndpointUnitTest<
        RefreshPlexAccountAccessEndpoint,
        RefreshPlexAccountAccessEndpointRequest,
        ResultDTO<List<RefreshPlexAccountAccessRapportDTO>>
    >
{
    [Test]
    [Arguments(0, TelemetryOutcome.Succeeded)]
    [Arguments(7, TelemetryOutcome.Succeeded)]
    [Arguments(0, TelemetryOutcome.Failed)]
    [Arguments(7, TelemetryOutcome.Failed)]
    [Arguments(7, TelemetryOutcome.Rejected)]
    [Arguments(7, TelemetryOutcome.Cancelled)]
    public async Task ShouldPreserveRefreshResponseAndCaptureOnlyUnexpectedErrors_WhenRefreshingAccounts(
        int accountId,
        TelemetryOutcome outcome
    )
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = "https://telemetry.example.test";
            build.TelemetryProjectToken = "phc_test";
        });
        const string identity = "581b32aa-0e38-49bd-b9ce-f5c639130bd5";
        var failure = new InvalidOperationException("Access refresh failed");
        var reports = new List<RefreshPlexAccountAccessRapportDTO> { new(7, "Target account") { Access = [] } };
        var commandResult = outcome switch
        {
            TelemetryOutcome.Failed => Result.Fail<List<RefreshPlexAccountAccessRapportDTO>>(
                new ExceptionalError(failure)
            ),
            TelemetryOutcome.Rejected => ResultExtensions
                .Create400BadRequestResult("Refresh rejected")
                .ToResult<List<RefreshPlexAccountAccessRapportDTO>>(),
            TelemetryOutcome.Cancelled => ResultExtensions
                .TaskIsCancelled("Access refresh")
                .ToResult<List<RefreshPlexAccountAccessRapportDTO>>(),
            _ => Result.Ok(reports),
        };
        var expectedStatus = outcome switch
        {
            TelemetryOutcome.Succeeded => StatusCodes.Status200OK,
            TelemetryOutcome.Rejected => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };
        var expectedEventOutcome = outcome switch
        {
            TelemetryOutcome.Succeeded => TelemetryOutcome.Accepted,
            TelemetryOutcome.Cancelled => TelemetryOutcome.Cancelled,
            _ => TelemetryOutcome.Failed,
        };
        int? expectedAccountId = accountId > 0 ? accountId : null;
        var expectedIssues = outcome == TelemetryOutcome.Failed ? 1 : 0;
        var user = new Mock<IUserService>(MockBehavior.Strict);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(identity).Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(
                    identity,
                    TelemetryProtocol.ProfileEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(
                    identity,
                    nameof(TelemetryEvent.PlexAccessRefreshRequested),
                    It.Is<Dictionary<string, object>>(properties =>
                        (string)properties[TelemetryProperty.Scope] == nameof(TelemetryScope.Account)
                        && (string)properties[TelemetryProperty.Trigger] == nameof(TelemetryTrigger.Manual)
                        && (string)properties[TelemetryProperty.Outcome] == expectedEventOutcome.ToString()
                        && (int?)properties[TelemetryProperty.AccountId] == expectedAccountId
                        && properties[TelemetryProperty.ServerId] == null
                    ),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        client
            .Setup(x =>
                x.CaptureException(
                    failure,
                    identity,
                    It.Is<Dictionary<string, object>>(properties =>
                        (string)properties[TelemetryProperty.Stage] == nameof(TelemetryActivity.PlexAccessRefresh)
                    ),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Exactly(expectedIssues));
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshPlexAccountAccessCommand>(command =>
                        command.PlexAccountId == accountId && command.Trigger == TelemetryTrigger.Manual
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once());
        await telemetry.InitializeAsync();

        // Act
        var response = await TestEndpointHandleAsync(
            new RefreshPlexAccountAccessEndpointRequest(accountId),
            extraServices => extraServices.AddSingleton<ITelemetry>(telemetry)
        );

        // Assert
        response.ValidationResult!.IsValid.ShouldBeTrue();
        response.ValidationErrors.Count.ShouldBe(0);
        response.StatusCode.ShouldBe(expectedStatus);
        response.Response.ShouldNotBeNull();
        response.Response.IsSuccess.ShouldBe(outcome == TelemetryOutcome.Succeeded);
        response.Response.Errors.Count.ShouldBe(outcome == TelemetryOutcome.Succeeded ? 0 : 1);
        if (outcome == TelemetryOutcome.Succeeded)
            response.Response.Value.ShouldBeEquivalentTo(reports);
        else
        {
            response.Response.Value.ShouldBeNull();
            response.Response.Errors.Single().Message.ShouldBe(commandResult.Errors.Single().Message);
        }
        client.Verify(
            x =>
                x.CaptureException(
                    It.IsAny<Exception>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                ),
            Times.Exactly(expectedIssues)
        );
        Mock.Mock<ICommandExecutor>().Verify();
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldRejectNegativeAccountId_WithoutRefreshingOrCapturingTelemetry()
    {
        // Arrange
        var request = new RefreshPlexAccountAccessEndpointRequest(-1);

        // Act
        var response = await TestEndpointHandleAsync(request);

        // Assert
        response.ValidationResult!.IsValid.ShouldBeFalse();
        response.ValidationErrors.Single().PropertyName.ShouldBe(nameof(request.PlexAccountId));
        response.Response.ShouldBeNull();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<RefreshPlexAccountAccessCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.IsAny<ResultBase>(), It.IsAny<TelemetryActivity>()), Times.Never());
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.PlexAccessRefreshRequested(
                        It.IsAny<TelemetryScope>(),
                        It.IsAny<Result<List<RefreshPlexAccountAccessRapportDTO>>>(),
                        It.IsAny<int?>(),
                        It.IsAny<int?>(),
                        It.IsAny<int>()
                    ),
                Times.Never()
            );
    }
}
