using LukeHagar.PlexAPI.SDK.Models.Requests;

namespace Reaparr.PlexApi;

public class ValidatePlexTokenCommandValidator : Validator<ValidatePlexTokenCommand>
{
    public ValidatePlexTokenCommandValidator()
    {
        RuleFor(x => x.AuthenticationToken).MinimumLength(5);
    }
}

public class ValidatePlexTvTokenCommandHandler
    : ICommandHandler<ValidatePlexTokenCommand, Result<ValidatePlexTokenCommandResult>>
{
    private readonly IPlexApiClientFactory _plexApiClientFactory;
    private readonly ILogger _log;

    public ValidatePlexTvTokenCommandHandler(ILogger log, IPlexApiClientFactory plexApiClientFactory)
    {
        _log = log.ForContext<ValidatePlexTvTokenCommandHandler>();

        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<ValidatePlexTokenCommandResult>> ExecuteAsync(
        ValidatePlexTokenCommand command,
        CancellationToken ct
    )
    {
        var clientId = Guid.NewGuid().ToString();
        var client = _plexApiClientFactory.CreateTvClient(command.AuthenticationToken, cancellationToken: ct);
        var response = await client.Authentication.GetTokenDetailsAsync(new GetTokenDetailsRequest()).ToResponse(ct);
        if (response.IsCancelledOrFailed)
            return response.ToResult();

        var isValid = response.Value.RawResponse.IsSuccessStatusCode;
        var result = response.ToApiResult(x => new ValidatePlexTokenCommandResult
        {
            ClientId = clientId,
            Username = x.UserPlexAccount!.Username,
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
            var username = response.Value.UserPlexAccount!.Username;
            _log.Here()
                .Information(
                    "Successfully retrieved the PlexAccount data for user {UserName} from the PlexApi",
                    username
                );
        }

        return result;
    }
}
