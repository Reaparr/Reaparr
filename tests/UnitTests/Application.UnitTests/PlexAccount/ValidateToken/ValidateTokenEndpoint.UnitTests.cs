namespace Reaparr.Application.UnitTests;

public class ValidatePlexTokenEndpointUnitTests
    : BaseEndpointUnitTest<
        ValidatePlexTokenEndpoint,
        ValidatePlexTokenEndpointRequest,
        ResultDTO<ValidatePlexTokenEndpointResponse>
    >
{
    [Test]
    public async Task ShouldValidateThePlexToken_WhenTokenIsValid()
    {
        // Arrange
        var seed = new Seed(2058);
        var testAccountDTO = FakeData.GetPlexAccount(seed).Generate().ToDTO();
        testAccountDTO.Username = "testuser";
        testAccountDTO.Password = string.Empty;
        testAccountDTO.CustomAuthenticationToken = "valid-token";

        var testAccountResponse = testAccountDTO.ToModel();
        testAccountResponse.UpdateInitProperty(nameof(testAccountResponse.ValidatedAt), DateTime.UtcNow);

        var commandResult = new ValidatePlexTokenCommandResult
        {
            ClientId = testAccountResponse.ClientId,
            Username = testAccountResponse.Username,
            Email = testAccountResponse.Email,
            Title = testAccountResponse.Title,
            PlexId = testAccountResponse.PlexId,
            Uuid = testAccountResponse.Uuid,
            AuthenticationToken = testAccountResponse.AuthenticationToken,
            IsValidated = true,
            ValidatedAt = DateTime.UtcNow,
            Is2Fa = false,
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(commandResult));

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new ValidatePlexTokenEndpointRequest
            {
                DisplayName = testAccountDTO.DisplayName,
                ManualAuthenticationToken = testAccountDTO.AuthenticationToken,
            }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.ShouldNotBeNull();
        value.IsUnAuthorized.ShouldBeFalse();
        value.IsValidated.ShouldBeTrue();
        value.ValidatedAt.ShouldNotBeNull();
        value.ValidatedAt?.ShouldBe(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        value.Is2Fa.ShouldBeFalse();
        value.Email.ShouldBe(testAccountDTO.Email);
        value.Username.ShouldBe(testAccountDTO.Username);
        value.CustomAuthenticationToken.ShouldNotBeEmpty();
        value.ClientId.ShouldBe(testAccountResponse.ClientId);
        value.Title.ShouldBe(testAccountResponse.Title);
        value.PlexId.ShouldBe(testAccountResponse.PlexId);
        value.Uuid.ShouldBe(testAccountResponse.Uuid);

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldPersistInvalidValidationResult_WhenExistingAccountIsUnauthorized()
    {
        // Arrange
        await SetupDatabase(93205, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.AsTracking().FirstAsync(CancellationToken);
        var originalUsername = plexAccount.Username;
        var originalToken = plexAccount.CustomAuthenticationToken;

        var commandResult = new ValidatePlexTokenCommandResult
        {
            ClientId = string.Empty,
            Username = "external-user-must-not-be-saved",
            Email = string.Empty,
            Title = string.Empty,
            PlexId = 0,
            Uuid = string.Empty,
            AuthenticationToken = "external-token-must-not-be-saved",
            IsValidated = false,
            ValidatedAt = null,
            Is2Fa = false,
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(commandResult).AddPlex401UnauthorizedError());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(RefreshDataType.PlexAccount))
            .Returns(Task.CompletedTask);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new ValidatePlexTokenEndpointRequest
            {
                PlexAccountId = plexAccount.Id,
                DisplayName = plexAccount.DisplayName,
                ManualAuthenticationToken = plexAccount.GetAuthToken,
            }
        );

        // Assert
        var endpointResponse = endpointResult.Response;
        endpointResponse.ShouldNotBeNull();
        endpointResponse.IsSuccess.ShouldBeTrue();
        var response = endpointResponse.Value;
        response.ShouldNotBeNull();
        response.IsUnAuthorized.ShouldBeTrue();

        var persistedAccount = await IDbContext.PlexAccounts.SingleAsync(
            x => x.Id == plexAccount.Id,
            CancellationToken
        );
        persistedAccount.IsValidated.ShouldBeFalse();
        persistedAccount.ValidatedAt.ShouldBeNull();
        persistedAccount.Username.ShouldBe(originalUsername);
        persistedAccount.CustomAuthenticationToken.ShouldBe(originalToken);

        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(RefreshDataType.PlexAccount), Times.Once());
    }

    [Test]
    public async Task ShouldMarkUnauthorized_WhenThePlexAPIRespondsWithA401()
    {
        // Arrange
        var seed = new Seed(203958);
        var testAccountDTO = FakeData.GetPlexAccount(seed).Generate().ToDTO();
        testAccountDTO.Username = "testuser";
        testAccountDTO.Password = string.Empty;
        testAccountDTO.CustomAuthenticationToken = "invalid-token";

        var commandResult = new ValidatePlexTokenCommandResult
        {
            ClientId = testAccountDTO.ClientId,
            Username = testAccountDTO.Username,
            Email = testAccountDTO.Email,
            Title = testAccountDTO.Title,
            PlexId = testAccountDTO.PlexId,
            Uuid = testAccountDTO.Uuid,
            AuthenticationToken = testAccountDTO.AuthenticationToken,
            IsValidated = false,
            ValidatedAt = null,
            Is2Fa = false,
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(commandResult).AddPlex401UnauthorizedError());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new ValidatePlexTokenEndpointRequest
            {
                DisplayName = testAccountDTO.DisplayName,
                ManualAuthenticationToken = testAccountDTO.AuthenticationToken,
            }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.ShouldNotBeNull();
        value.IsUnAuthorized.ShouldBeTrue();
        value.IsValidated.ShouldBeFalse();
        value.ValidatedAt.ShouldBeNull();
        value.Is2Fa.ShouldBeFalse();
        value.Username.ShouldBeEmpty();
        value.Email.ShouldBeEmpty();
        value.ClientId.ShouldBeEmpty();
        value.Title.ShouldBeEmpty();
        value.PlexId.ShouldBe(0);
        value.Uuid.ShouldBeEmpty();
        value.CustomAuthenticationToken.ShouldBeEmpty();

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldHandle2FaEnabledScenario_WhenTokenIsValid()
    {
        // Arrange
        var seed = new Seed(2059);
        var testAccountDTO = FakeData.GetPlexAccount(seed).Generate().ToDTO();
        testAccountDTO.Username = "testuser2fa";
        testAccountDTO.Password = string.Empty;
        testAccountDTO.CustomAuthenticationToken = "valid-2fa-token";

        var commandResult = new ValidatePlexTokenCommandResult
        {
            ClientId = testAccountDTO.ClientId,
            Username = testAccountDTO.Username,
            Email = testAccountDTO.Email,
            Title = testAccountDTO.Title,
            PlexId = testAccountDTO.PlexId,
            Uuid = testAccountDTO.Uuid,
            AuthenticationToken = testAccountDTO.AuthenticationToken,
            IsValidated = true,
            ValidatedAt = DateTime.UtcNow,
            Is2Fa = true,
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(commandResult));

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new ValidatePlexTokenEndpointRequest
            {
                DisplayName = testAccountDTO.DisplayName,
                ManualAuthenticationToken = testAccountDTO.AuthenticationToken,
            }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.ShouldNotBeNull();
        value.IsUnAuthorized.ShouldBeFalse();
        value.IsValidated.ShouldBeTrue();
        value.ValidatedAt.ShouldNotBeNull();
        value.ValidatedAt?.ShouldBe(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        value.Is2Fa.ShouldBeTrue();
        value.Email.ShouldBe(testAccountDTO.Email);
        value.Username.ShouldBe(testAccountDTO.Username);
        value.CustomAuthenticationToken.ShouldNotBeEmpty();

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldNotMarkUnauthorized_WhenUnhandledPlexErrorsOccur()
    {
        // Arrange
        var seed = new Seed(203960);
        var testAccountDTO = FakeData.GetPlexAccount(seed).Generate().ToDTO();
        testAccountDTO.Username = "testuser";
        testAccountDTO.Password = string.Empty;
        testAccountDTO.CustomAuthenticationToken = "error-token";

        var commandResult = new ValidatePlexTokenCommandResult
        {
            ClientId = testAccountDTO.ClientId,
            Username = testAccountDTO.Username,
            Email = testAccountDTO.Email,
            Title = testAccountDTO.Title,
            PlexId = testAccountDTO.PlexId,
            Uuid = testAccountDTO.Uuid,
            AuthenticationToken = testAccountDTO.AuthenticationToken,
            IsValidated = false,
            ValidatedAt = null,
            Is2Fa = false,
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result
                    .Ok(commandResult)
                    .WithErrors([new PlexError("Error #1") { Code = 1234 }, new PlexError("Error #2") { Code = 5678 }])
            );

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new ValidatePlexTokenEndpointRequest
            {
                DisplayName = testAccountDTO.DisplayName,
                ManualAuthenticationToken = testAccountDTO.AuthenticationToken,
            }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Count.ShouldBe(2);

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenCommandExecutionFails()
    {
        // Arrange
        var seed = new Seed(203961);
        var testAccountDTO = FakeData.GetPlexAccount(seed).Generate().ToDTO();
        testAccountDTO.Username = "testuser";
        testAccountDTO.Password = string.Empty;
        testAccountDTO.CustomAuthenticationToken = "failing-token";

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Command execution failed"));

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new ValidatePlexTokenEndpointRequest
            {
                DisplayName = testAccountDTO.DisplayName,
                ManualAuthenticationToken = testAccountDTO.AuthenticationToken,
            }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldPreserveUnvalidatedEnvelope_WhenDisplayNameIsNotProvided()
    {
        // Arrange
        var seed = new Seed(2058);
        var testAccountDTO = FakeData.GetPlexAccount(seed).Generate().ToDTO();
        testAccountDTO.Username = "testuser";
        testAccountDTO.Password = string.Empty;
        testAccountDTO.CustomAuthenticationToken = "valid-token";

        var commandResult = Result.Ok(
            new ValidatePlexTokenCommandResult
            {
                ClientId = testAccountDTO.ClientId,
                Username = testAccountDTO.Username,
                Email = testAccountDTO.Email,
                Title = testAccountDTO.Title,
                PlexId = testAccountDTO.PlexId,
                Uuid = testAccountDTO.Uuid,
                AuthenticationToken = testAccountDTO.AuthenticationToken,
                IsValidated = false,
                ValidatedAt = null,
                Is2Fa = false,
            }
        );

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ValidatePlexTokenCommand>(command =>
                        command.AuthenticationToken == testAccountDTO.AuthenticationToken
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new ValidatePlexTokenEndpointRequest
            {
                PlexAccountId = 0,
                DisplayName = "UnknownDisplayName", // Default value
                ManualAuthenticationToken = testAccountDTO.AuthenticationToken,
            }
        );
        var result = endpointResult.Response;

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var value = result.Value;
        value.ShouldNotBeNull();
        value.IsValidated.ShouldBeFalse();
        value.ValidatedAt.ShouldBeNull();

        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.PlexAuthenticationProcessed(
                        0,
                        It.Is<ResultBase>(telemetryResult => ReferenceEquals(telemetryResult, commandResult)),
                        It.Is<PlexAuthenticationTelemetryDTO>(payload => !payload.IsValidated)
                    ),
                Times.Once()
            );
    }
}
