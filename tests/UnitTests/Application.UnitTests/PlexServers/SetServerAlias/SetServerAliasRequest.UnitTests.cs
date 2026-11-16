using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Application.UnitTests;

public class SetServerAliasRequestUnitTests : BaseEndpointUnitTest<SetServerAlias, SetServerAliasRequest, BaseResultDTO>
{
    [Test]
    public async Task ShouldRejectEmptyAlias_WhenSettingServerAlias()
    {
        // Arrange
        var request = new SetServerAliasRequest { PlexServerId = 1, ServerAlias = string.Empty };

        // Act
        var result = await TestEndpointHandleAsync(request, services =>
            services.AddSingleton(_ => Mock.Mock<IServerSettingsModule>().Object)
        );

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Response.ShouldBeNull();
        result.ValidationResult.ShouldNotBeNull();
        result.ValidationResult.Errors.ShouldContain(x => x.PropertyName == nameof(SetServerAliasRequest.ServerAlias));
    }

    [Test]
    public async Task ShouldRejectWhitespaceAlias_WhenSettingServerAlias()
    {
        // Arrange
        var request = new SetServerAliasRequest { PlexServerId = 1, ServerAlias = "   " };

        // Act
        var result = await TestEndpointHandleAsync(request, services =>
            services.AddSingleton(_ => Mock.Mock<IServerSettingsModule>().Object)
        );

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Response.ShouldBeNull();
        result.ValidationResult.ShouldNotBeNull();
        result.ValidationResult.Errors.ShouldContain(x => x.PropertyName == nameof(SetServerAliasRequest.ServerAlias));
    }
}
