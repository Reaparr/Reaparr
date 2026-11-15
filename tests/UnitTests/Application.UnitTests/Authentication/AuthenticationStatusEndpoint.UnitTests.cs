using System.Security.Claims;

namespace Reaparr.Application.UnitTests;

public class AuthenticationStatusEndpointUnitTests
    : BaseEndpointWithoutRequestUnitTest<AuthenticationStatusEndpoint, UserClaimsDTO>
{
    [Test]
    public async Task ShouldReturnAuthenticatedStatusWithTelemetryBootstrap_WhenTelemetryIsAvailable()
    {
        // Arrange
        var bootstrap = new BrowserTelemetryBootstrapDTO(
            "https://telemetry.example.test",
            "phc_test",
            "5e2c5c48-aa0f-4f21-8c65-94e72b2bd389",
            "0.40.0-dev.1",
            TelemetryProtocol.DevelopmentEnvironment,
            "desktop",
            "linux-x64",
            nameof(OperatingSystemPlatform.Linux)
        );
        Mock.Mock<ITelemetry>()
            .Setup(x => x.GetBrowserBootstrap())
            .Returns(bootstrap)
            .Verifiable(Times.Once());
        var endpoint = SetupEndpointUnitTest<AuthenticationStatusEndpoint>();
        endpoint.HttpContext.Response.Body = new MemoryStream();
        endpoint.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "jason"), new Claim(ClaimTypes.Role, "Admin")],
                "TestAuth"
            )
        );

        // Act
        await endpoint.HandleAsync(CancellationToken);

        // Assert
        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<ResultDTO<UserClaimsDTO>>(
                endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var value = response.Value.ShouldNotBeNull();
        value.IsLoggedIn.ShouldBeTrue();
        value.UserName.ShouldBe("jason");
        value.Claims.ShouldBe([ClaimTypes.Name, ClaimTypes.Role]);
        value.Telemetry.ShouldBe(bootstrap);
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldReturnAuthenticatedStatusWithoutTelemetry_WhenTelemetryIsUnavailable()
    {
        // Arrange
        Mock.Mock<ITelemetry>()
            .Setup(x => x.GetBrowserBootstrap())
            .Returns((BrowserTelemetryBootstrapDTO?)null)
            .Verifiable(Times.Once());
        var endpoint = SetupEndpointUnitTest<AuthenticationStatusEndpoint>();
        endpoint.HttpContext.Response.Body = new MemoryStream();
        endpoint.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, "jason")], "TestAuth")
        );

        // Act
        await endpoint.HandleAsync(CancellationToken);

        // Assert
        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<ResultDTO<UserClaimsDTO>>(
                endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response.Value.ShouldNotBeNull().Telemetry.ShouldBeNull();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldReturnUnauthorizedWithoutQueryingTelemetry_WhenUserIsAnonymous()
    {
        // Arrange
        var endpoint = SetupEndpointUnitTest<AuthenticationStatusEndpoint>();
        endpoint.HttpContext.Response.Body = new MemoryStream();
        endpoint.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        await endpoint.HandleAsync(CancellationToken);

        // Assert
        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<BaseResultDTO>(
                endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeFalse();
        response.Errors.Count.ShouldBe(1);
        Mock.Mock<ITelemetry>().Verify(x => x.GetBrowserBootstrap(), Times.Never());
    }
}
