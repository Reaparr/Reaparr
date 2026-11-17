using LukeHagar.PlexAPI.SDK.Models.Requests;

namespace Reaparr.PlexApi;

public class ValidatePlexAccountEndpointRequestValidator : Validator<PlexSignInCommand>
{
    public ValidatePlexAccountEndpointRequestValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().MinimumLength(5);

        RuleFor(x => x.Username).NotEmpty().MinimumLength(5);

        RuleFor(x => x.Password).NotEmpty().MinimumLength(5);

        RuleFor(x => x.VerificationCode).Length(6).When(m => !string.IsNullOrEmpty(m.VerificationCode));
    }
}

public class PlexSignInCommandHandler : ICommandHandler<PlexSignInCommand, Result<PlexSignInCommandResult>>
{
    private readonly ILogger _log;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public PlexSignInCommandHandler(ILogger log, IPlexApiClientFactory plexApiClientFactory)
    {
        _log = log.ForContext<PlexSignInCommandHandler>();
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<PlexSignInCommandResult>> ExecuteAsync(PlexSignInCommand command, CancellationToken ct)
    {
        _log.Here().Debug("Requesting PlexToken for account {UserName}", command.Username);
        var plexTvClient = _plexApiClientFactory.CreateTvClient(cancellationToken: ct);
        var response = await plexTvClient
            .Authentication.PostUsersSignInDataAsync(
                new PostUsersSignInDataRequest
                {
                    ClientIdentifier = command.ClientId,
                    RequestBody = new PostUsersSignInDataRequestBody
                    {
                        Login = command.Username,
                        Password = command.Password,
                        RememberMe = false,
                        VerificationCode = command.VerificationCode,
                    },
                }
            )
            .ToResponse(ct);

        if (response.IsCancelledOrFailed)
            return response.ToResult().LogIfFailed();

        var isValid = response.Value.RawResponse.IsSuccessStatusCode;
        var result = response.ToApiResult(x => new PlexSignInCommandResult
        {
            ClientId = command.ClientId,
            Username = command.Username,
            Password = command.Password,

            PlexId = x.UserPlexAccount!.Id,
            Uuid = x.UserPlexAccount!.Uuid,
            IsValidated = isValid,
            ValidatedAt = isValid ? DateTime.UtcNow : null,

            Title = x.UserPlexAccount!.Title,
            Email = x.UserPlexAccount!.Email,
            AuthenticationToken = x.UserPlexAccount!.AuthToken,
            Is2Fa = x.UserPlexAccount!.TwoFactorEnabled.GetValueOrDefault(),
        });

        if (result.IsSuccess)
        {
            _log.Here()
                .Information(
                    "Successfully retrieved the PlexAccount data for user {UserName} from the PlexApi",
                    command.Username
                );
        }

        return result;
    }
}
