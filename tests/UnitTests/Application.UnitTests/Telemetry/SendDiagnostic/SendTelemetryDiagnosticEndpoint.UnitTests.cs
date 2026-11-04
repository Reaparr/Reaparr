using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Reaparr.Domain;
using Reaparr.Identity.Contracts;

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
        var request = JsonSerializer.Deserialize<SendTelemetryDiagnosticRequest>(json, DefaultJsonSerializerOptions.ConfigStandard)!;

        // Act
        var result = await TestEndpointHandleAsync(request, services => services.AddSingleton(Mock.Mock<ITelemetry>().Object));

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
        result.ValidationErrors.Single().PropertyName.ShouldBe(nameof(SendTelemetryDiagnosticRequest.Origin));
        Mock.Mock<ITelemetry>().Verify(x => x.TrackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>()), Times.Never());
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
        Mock.Mock<ITelemetry>().Verify(x => x.TrackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>()), Times.Never());
    }

    [Test]
    public async Task ShouldRejectAnonymousCaller_WhenNoAuthenticationExists()
    {
        // Arrange
        var request = new SendTelemetryDiagnosticRequest { Origin = TelemetryDiagnosticOrigin.Backend };

        // Act
        var result = await TestEndpointHandleAsync(request, services => services.AddSingleton(Mock.Mock<ITelemetry>().Object));

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(401);
        result.Response!.IsSuccess.ShouldBeFalse();
        result.Response.Errors.Count.ShouldBe(1);
        Mock.Mock<ITelemetry>().Verify(x => x.TrackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>()), Times.Never());
    }

    [Test]
    [Arguments("admin")]
    [Arguments("")]
    public async Task ShouldRejectNonAdministrator_WhenThePrincipalIsAuthenticated(string name)
    {
        // Arrange
        var endpoint = SetupEndpointUnitTest<SendTelemetryDiagnosticEndpoint>(services => services.AddSingleton(Mock.Mock<ITelemetry>().Object));
        endpoint.HttpContext.Response.Body = new MemoryStream();
        var claims = new List<Claim> { new(ClaimTypes.Name, name) };
        endpoint.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var request = new SendTelemetryDiagnosticRequest { Origin = TelemetryDiagnosticOrigin.Backend };
        var validation = await ValidateEndpointRequestAsync(request, CancellationToken);

        // Act
        await endpoint.HandleAsync(request, CancellationToken);

        // Assert
        validation!.IsValid.ShouldBeTrue();
        endpoint.HttpContext.Response.StatusCode.ShouldBe(403);
        var response = await GetEndpointResponseAsync(endpoint, CancellationToken);
        response!.IsSuccess.ShouldBeFalse();
        response.Errors.Count.ShouldBe(1);
        Mock.Mock<ITelemetry>().Verify(x => x.TrackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>()), Times.Never());
    }

    [Test]
    [Arguments(TelemetryDiagnosticOrigin.Backend)]
    [Arguments(TelemetryDiagnosticOrigin.Frontend)]
    public async Task ShouldAllowAdministratorWithoutANameClaim_WhenSubmittingDiagnostic(TelemetryDiagnosticOrigin origin)
    {
        // Arrange
        var endpoint = SetupEndpointUnitTest<SendTelemetryDiagnosticEndpoint>(services => services.AddSingleton(Mock.Mock<ITelemetry>().Object));
        endpoint.HttpContext.Response.Body = new MemoryStream();
        endpoint.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, DefaultUserAppCredentials.DefaultAdminRole),
        ], "test"));
        var request = JsonSerializer.Deserialize<SendTelemetryDiagnosticRequest>(
            $"{{\"origin\":{(int)origin}}}", DefaultJsonSerializerOptions.ConfigStandard)!;
        var validation = await ValidateEndpointRequestAsync(request, CancellationToken);
        Mock.Mock<ITelemetry>().Setup(x => x.TrackAsync(
                "reaparr diagnostic",
                It.Is<IReadOnlyDictionary<string, object?>>(p => p.Count == 1 && (TelemetryDiagnosticOrigin)p["origin"]! == origin)))
            .ReturnsAsync(Result.Ok().WithSuccess("Diagnostic event accepted for sending")).Verifiable(Times.Once());

        // Act
        await endpoint.HandleAsync(request, CancellationToken);

        // Assert
        validation!.IsValid.ShouldBeTrue();
        endpoint.HttpContext.Response.StatusCode.ShouldBe(200);
        var result = await GetEndpointResponseAsync(endpoint, CancellationToken);
        result!.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Successes.Single().Message.ShouldBe("Diagnostic event accepted for sending");
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    [Arguments(403)]
    [Arguments(503)]
    public async Task ShouldExposeSafeFailureStatus_WhenTelemetryCannotSubmit(int status)
    {
        // Arrange
        var endpoint = SetupEndpointUnitTest<SendTelemetryDiagnosticEndpoint>(services => services.AddSingleton(Mock.Mock<ITelemetry>().Object));
        endpoint.HttpContext.Response.Body = new MemoryStream();
        endpoint.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, DefaultUserAppCredentials.DefaultAdminRole),
        ], "test"));
        var request = new SendTelemetryDiagnosticRequest { Origin = TelemetryDiagnosticOrigin.Backend };
        var validation = await ValidateEndpointRequestAsync(request, CancellationToken);
        Mock.Mock<ITelemetry>().Setup(x => x.TrackAsync(
                "reaparr diagnostic",
                It.Is<IReadOnlyDictionary<string, object?>>(p => p.Count == 1 && (TelemetryDiagnosticOrigin)p["origin"]! == TelemetryDiagnosticOrigin.Backend)))
            .ReturnsAsync(Result.Fail("Telemetry is unavailable").AddStatusCode((System.Net.HttpStatusCode)status, "Telemetry is unavailable"))
            .Verifiable(Times.Once());

        // Act
        await endpoint.HandleAsync(request, CancellationToken);

        // Assert
        validation!.IsValid.ShouldBeTrue();
        endpoint.HttpContext.Response.StatusCode.ShouldBe(status);
        var response = await GetEndpointResponseAsync(endpoint, CancellationToken);
        response!.IsSuccess.ShouldBeFalse();
        response.Errors.Count.ShouldBe(1);
        Mock.Mock<ITelemetry>().Verify();
    }
}
