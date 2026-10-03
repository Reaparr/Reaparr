using LukeHagar.PlexAPI.SDK.Models.Requests;

namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    public static Faker<PostUsersSignInDataUserPlexAccount> GetPlexSignInResponse(
        Seed seed,
        Action<PlexApiDataConfig>? options = null
    )
    {
        return new Faker<PostUsersSignInDataUserPlexAccount>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.Id, _ => GetUniqueNumber())
            .RuleFor(x => x.Uuid, f => f.Random.Guid().ToString())
            .RuleFor(x => x.Username, f => f.Internet.UserName())
            .RuleFor(x => x.Title, f => f.Internet.UserName())
            .RuleFor(x => x.Email, f => f.Internet.Email())
            .RuleFor(x => x.FriendlyName, _ => "")
            .RuleFor(x => x.Locale, _ => "EN")
            .RuleFor(x => x.Confirmed, f => f.Random.Bool())
            .RuleFor(x => x.JoinedAt, f => f.Date.Past(3).Ticks)
            .RuleFor(x => x.EmailOnlyAuth, f => f.Random.Bool())
            .RuleFor(x => x.HasPassword, _ => true)
            .RuleFor(x => x.Protected, f => f.Random.Bool())
            .RuleFor(x => x.Thumb, f => f.Internet.UrlWithPath())
            .RuleFor(x => x.AuthToken, f => f.Random.Guid().ToString())
            .RuleFor(x => x.MailingListStatus, _ => MailingListStatus.Active)
            .RuleFor(x => x.MailingListActive, f => f.Random.Bool())
            .RuleFor(x => x.ScrobbleTypes, _ => "")
            .RuleFor(x => x.Country, _ => "EN")
            .RuleFor(x => x.Subscription, _ => new Subscription())
            .RuleFor(x => x.SubscriptionDescription, _ => null)
            .RuleFor(x => x.Restricted, f => f.Random.Bool())
            .RuleFor(x => x.Anonymous, _ => null)
            .RuleFor(x => x.Home, f => f.Random.Bool())
            .RuleFor(x => x.Guest, f => f.Random.Bool())
            .RuleFor(x => x.HomeSize, f => f.Random.Number(20))
            .RuleFor(x => x.HomeAdmin, f => f.Random.Bool())
            .RuleFor(x => x.MaxHomeSize, f => f.Random.Number(20))
            .RuleFor(x => x.RememberExpiresAt, f => f.Date.Future().Ticks)
#pragma warning disable CS8603 // Possible null reference return.
            .RuleFor(x => x.Profile, _ => null) // keep null, causes enum conversion exceptions in PlexAPI.SDK
#pragma warning restore CS8603 // Possible null reference return.
            .RuleFor(x => x.Entitlements, _ => [])
            .RuleFor(x => x.Subscriptions, _ => [])
            .RuleFor(x => x.PastSubscriptions, _ => [])
            .RuleFor(x => x.Trials, _ => [])
            .RuleFor(x => x.Services, _ => [])
            .RuleFor(x => x.AdsConsent, _ => null)
            .RuleFor(x => x.AdsConsentSetAt, _ => null)
            .RuleFor(x => x.AdsConsentReminderAt, _ => null)
#pragma warning disable CS0618 // Type or member is obsolete
            .RuleFor(x => x.Pin, _ => string.Empty)
#pragma warning restore CS0618 // Type or member is obsolete
            .RuleFor(x => x.AttributionPartner, _ => null)
            .RuleFor(x => x.Roles, _ => [""])
            .RuleFor(x => x.ExperimentalFeatures, f => f.Random.Bool())
            .RuleFor(x => x.TwoFactorEnabled, f => f.Random.Bool())
            .Ignore(x => x.AdditionalProperties)
            .RuleFor(x => x.BackupCodesCreated, f => f.Random.Bool());
    }

    public static PlexErrorsResponseDTO GetFailedPlexSignInResponse() =>
        new()
        {
            Errors =
            [
                new PlexErrorDTO
                {
                    Code = 1001,
                    Message = "User could not be authenticated",
                    Status = 401,
                },
            ],
        };
}
