using System.Security.Claims;

namespace Reaparr.Application.UnitTests;

public class AuthenticationStatusEndpointUnitTests
    : BaseEndpointWithoutRequestUnitTest<AuthenticationStatusEndpoint, UserClaimsDTO>
{
    [Test]
    public async Task ShouldReturnAuthenticatedStatus_WhenUserIsAuthenticated()
    {
        // Arrange
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
        var value = response.Value.ShouldNotBeNull();
        value.IsLoggedIn.ShouldBeTrue();
        value.UserName.ShouldBe("jason");
        value.Claims.ShouldBe([ClaimTypes.Name]);
    }

    [Test]
    public async Task ShouldReturnUnauthorized_WhenUserIsAnonymous()
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
    }
}
