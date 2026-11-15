using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Application.UnitTests;

public class SendTelemetryDiagnosticEndpointUnitTests
    : BaseEndpointUnitTest<SendTelemetryDiagnosticEndpoint, SendTelemetryDiagnosticRequest, BaseResultDTO>
{
    [Test]
    [Arguments("{}")]
    [Arguments("{\"origin\":null}")]
    [Arguments("{\"origin\":-1}")]
    [Arguments("{\"origin\":2}")]
    public async Task ShouldRejectMissingOrUnknownOrigin_WhenRequestIsInvalid(string json)
    {
        // Arrange
        var request = JsonSerializer.Deserialize<SendTelemetryDiagnosticRequest>(
            json,
            DefaultJsonSerializerOptions.ConfigStandard
        )!;

        // Act
        var result = await TestEndpointHandleAsync(
            request,
            services => services.AddSingleton(Mock.Mock<ITelemetry>().Object)
        );

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
        result.ValidationErrors.Single().PropertyName.ShouldBe(nameof(SendTelemetryDiagnosticRequest.Origin));
        Mock.Mock<ITelemetry>().Verify(x => x.SendDiagnostic(It.IsAny<TelemetryDiagnosticOrigin>()), Times.Never());
    }

    [Test]
    [Arguments("{\"origin\":0,\"accountUuid\":\"attacker\"}")]
    [Arguments("{\"origin\":0,\"event\":\"other\"}")]
    [Arguments("{\"origin\":0,\"properties\":{\"url\":\"https://private\"}}")]
    [Arguments("{\"origin\":0,\"host\":\"https://other\"}")]
    public void ShouldRejectAdditionalInputs_WhenJsonContainsUnapprovedFields(string json)
    {
        // Arrange
        var options = DefaultJsonSerializerOptions.ConfigStandard;

        // Act
        var act = () => JsonSerializer.Deserialize<SendTelemetryDiagnosticRequest>(json, options);

        // Assert
        Should.Throw<JsonException>(act);
        Mock.Mock<ITelemetry>().Verify(x => x.SendDiagnostic(It.IsAny<TelemetryDiagnosticOrigin>()), Times.Never());
    }

    [Test]
    [Arguments(TelemetryDiagnosticOrigin.Backend)]
    [Arguments(TelemetryDiagnosticOrigin.Frontend)]
    public async Task ShouldForwardDiagnosticAndExposeSuccess_WhenTelemetryAccepts(TelemetryDiagnosticOrigin origin)
    {
        // Arrange
        var request = new SendTelemetryDiagnosticRequest { Origin = origin };
        Mock.Mock<ITelemetry>()
            .Setup(x => x.SendDiagnostic(origin))
            .Returns(Result.Ok().WithSuccess("Diagnostic event accepted for sending"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestEndpointHandleAsync(
            request,
            services => services.AddSingleton(Mock.Mock<ITelemetry>().Object)
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(200);
        result.Response!.IsSuccess.ShouldBeTrue();
        result.Response.Errors.Count.ShouldBe(0);
        result.Response.Successes.Single().Message.ShouldBe("Diagnostic event accepted for sending");
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    [Arguments(403)]
    [Arguments(503)]
    public async Task ShouldForwardTelemetryFailureStatus_WhenTelemetryCannotSubmit(int status)
    {
        // Arrange
        var request = new SendTelemetryDiagnosticRequest { Origin = TelemetryDiagnosticOrigin.Backend };
        Mock.Mock<ITelemetry>()
            .Setup(x => x.SendDiagnostic(TelemetryDiagnosticOrigin.Backend))
            .Returns(
                Result
                    .Fail("Telemetry is unavailable")
                    .AddStatusCode((System.Net.HttpStatusCode)status, "Telemetry is unavailable")
            )
            .Verifiable(Times.Once());

        // Act
        var result = await TestEndpointHandleAsync(
            request,
            services => services.AddSingleton(Mock.Mock<ITelemetry>().Object)
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(status);
        result.Response!.IsSuccess.ShouldBeFalse();
        result.Response.Errors.Count.ShouldBe(1);
        Mock.Mock<ITelemetry>().Verify();
    }
}
