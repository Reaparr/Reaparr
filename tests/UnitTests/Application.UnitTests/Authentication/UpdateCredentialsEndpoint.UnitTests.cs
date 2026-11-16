using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Reaparr.Identity.Contracts;

namespace Reaparr.Application.UnitTests;

public class UpdateCredentialsEndpointUnitTests
    : BaseEndpointUnitTest<UpdateCredentialsEndpoint, UpdateCredentialsEndpointRequest>
{
    [Test]
    public async Task ShouldReportPartialOutcome_WhenUsernamePersistsButPasswordResetFails()
    {
        // Arrange
        var user = new AppUser { UserName = "old-user" };
        Mock.Mock<IUserService>()
            .Setup(x => x.GetFirstUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(user)
            .Verifiable(Times.Once());
        Mock.Mock<IUserService>()
            .Setup(x => x.ValidatePasswordAsync(user, "new-password"))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable(Times.Once());
        Mock.Mock<IUserService>()
            .Setup(x => x.UpdateAsync(It.Is<AppUser>(value => value.UserName == "new-user")))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable(Times.Once());
        Mock.Mock<IUserService>()
            .Setup(x => x.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("reset-token")
            .Verifiable(Times.Once());
        Mock.Mock<IUserService>()
            .Setup(x => x.ResetPasswordAsync(user, "reset-token", "new-password"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "reset failed" }))
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CredentialsUpdateProcessed(It.Is<ResultBase>(result => result.IsFailed), true, false))
            .Verifiable(Times.Once());
        var request = new UpdateCredentialsEndpointRequest { Username = "new-user", Password = "new-password" };

        // Act
        var outcome = await TestEndpointHandleAsync(request, services =>
            services.AddSingleton(Mock.Mock<IUserService>().Object)
        );

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<BaseResultDTO>(
                outcome.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeFalse();
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldHaveSingleItem().Message.ShouldBe("reset failed");
        user.UserName.ShouldBe("new-user");
        user.NormalizedUserName.ShouldBe("NEW-USER");
        Mock.Mock<IUserService>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldReportFailure_WhenPasswordResetFailsWithoutUsernameChange()
    {
        // Arrange
        var user = new AppUser { UserName = "old-user" };
        Mock.Mock<IUserService>()
            .Setup(x => x.GetFirstUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(user)
            .Verifiable(Times.Once());
        Mock.Mock<IUserService>()
            .Setup(x => x.ValidatePasswordAsync(user, "new-password"))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable(Times.Once());
        Mock.Mock<IUserService>()
            .Setup(x => x.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("reset-token")
            .Verifiable(Times.Once());
        Mock.Mock<IUserService>()
            .Setup(x => x.ResetPasswordAsync(user, "reset-token", "new-password"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "reset failed" }))
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CredentialsUpdateProcessed(It.Is<ResultBase>(result => result.IsFailed), false, false))
            .Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(
            new UpdateCredentialsEndpointRequest { Username = null, Password = "new-password" },
            services => services.AddSingleton(Mock.Mock<IUserService>().Object)
        );

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<BaseResultDTO>(
                outcome.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeFalse();
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        response.Errors.ShouldHaveSingleItem().Message.ShouldBe("reset failed");
        user.UserName.ShouldBe("old-user");
        Mock.Mock<IUserService>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }
}
