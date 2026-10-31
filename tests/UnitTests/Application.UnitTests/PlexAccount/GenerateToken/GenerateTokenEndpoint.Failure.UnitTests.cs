namespace Reaparr.Application.UnitTests;

public class GenerateTokenEndpointFailureUnitTests
    : BaseEndpointUnitTest<
        GeneratePlexTokenEndpoint,
        GeneratePlexTokenEndpointRequest,
        ResultDTO<GeneratePlexTokenResponse>
    >
{
    public enum SignInFailure
    {
        Generic,
        Transport,
        Validation,
        Cancelled,
    }

    [Test]
    [Arguments(SignInFailure.Generic)]
    [Arguments(SignInFailure.Transport)]
    [Arguments(SignInFailure.Validation)]
    [Arguments(SignInFailure.Cancelled)]
    public async Task ShouldPreserveFailedSignInResponses_WhenGeneratingAToken(SignInFailure kind)
    {
        // Arrange
        var (failure, expectedStatusCode) = CreateFailure(kind);

        // Act
        var endpointResult = await ExecuteFailure(failure);
        var result = endpointResult.Response;

        // Assert
        endpointResult.StatusCode.ShouldBe(expectedStatusCode);
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(expectedStatusCode);
        result.Value.ShouldBeNull();
        result.Errors.Count.ShouldBe(kind is SignInFailure.Generic or SignInFailure.Transport ? 2 : 1);

        switch (kind)
        {
            case SignInFailure.Generic:
                result.Errors.Select(x => x.Message).ShouldBe(["Domain sign-in failure", "Plex sign-in failure"]);
                result.Errors[0].Metadata["domain-code"].ToString().ShouldBe("domain-42");
                result.Errors[1].Metadata["plex-code"].ToString().ShouldBe("plex-321");
                break;
            case SignInFailure.Transport:
                result.Errors.Select(x => x.Message).ShouldBe(["Plex transport failure", "Bad Gateway"]);
                result.Errors[0].Metadata["transport"].ToString().ShouldBe("plex");
                result.Errors[1].Metadata[ResultExtensions.StatusCodeName].ToString().ShouldBe("502");
                break;
            case SignInFailure.Validation:
                result.Errors[0].Message.ShouldBe("Fluent Validation Pipeline Failed.");
                result.Errors[0].Metadata[ResultExtensions.StatusCodeName].ToString().ShouldBe("400");
                result.Errors[0].Reasons.Select(x => x.Message).ShouldBe(["Username is required"]);
                break;
            case SignInFailure.Cancelled:
                result.Errors[0].Message.ShouldBe("Plex sign-in was cancelled");
                result.Errors[0].Metadata["operation"].ToString().ShouldBe("PlexSignIn");
                break;
        }
    }

    private static (Result<PlexSignInCommandResult> Failure, int StatusCode) CreateFailure(SignInFailure kind)
    {
        switch (kind)
        {
            case SignInFailure.Generic:
                var domainError = new Error("Domain sign-in failure").WithMetadata("domain-code", "domain-42");
                var plexError = new PlexError("Plex sign-in failure") { Code = 321, Status = 400 };
                plexError.WithMetadata("plex-code", "plex-321");
                return (
                    Result.Fail<PlexSignInCommandResult>(domainError).WithError(plexError),
                    StatusCodes.Status500InternalServerError
                );
            case SignInFailure.Transport:
                var transportError = new ExceptionalError(
                    "Plex transport failure",
                    new HttpRequestException("Plex endpoint was unreachable")
                );
                transportError.WithMetadata("transport", "plex");
                return (
                    Result.Fail<PlexSignInCommandResult>(transportError).Add502BadGatewayError(),
                    StatusCodes.Status502BadGateway
                );
            case SignInFailure.Validation:
                var validation = new FluentValidation.Results.ValidationResult([
                    new FluentValidation.Results.ValidationFailure(
                        nameof(PlexSignInCommand.Username),
                        "Username is required"
                    ),
                ]);
                return (validation.ToResult<Result<PlexSignInCommandResult>>(), StatusCodes.Status400BadRequest);
            case SignInFailure.Cancelled:
                var cancellationError = new ExceptionalError(
                    "Plex sign-in was cancelled",
                    new OperationCanceledException("The Plex sign-in operation was cancelled")
                );
                cancellationError.WithMetadata("operation", "PlexSignIn");
                return (
                    Result.Fail<PlexSignInCommandResult>(cancellationError),
                    StatusCodes.Status500InternalServerError
                );
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private async Task<
        EndpointUnitTestResult<GeneratePlexTokenEndpoint, ResultDTO<GeneratePlexTokenResponse>>
    > ExecuteFailure(Result<PlexSignInCommandResult> failure)
    {
        await SetupDatabase(433223, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.SingleAsync(CancellationToken);
        Mock.SetupCommand(It.IsAny<PlexSignInCommand>).ReturnsAsync(failure);
        return await TestEndpointHandleAsync(new GeneratePlexTokenEndpointRequest { PlexAccountId = plexAccount.Id });
    }
}
