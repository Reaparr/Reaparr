namespace Reaparr.BaseTests;

public partial class FakeData
{
    private static readonly Faker<PlexAccount> _plexAccountFaker = new Faker<PlexAccount>()
        .StrictMode(true)
        .Ignore(x => x.Id)
        .RuleFor(x => x.DisplayName, f => f.Internet.UserName())
        .RuleFor(x => x.Username, f => f.Internet.UserName())
        .RuleFor(x => x.Password, f => f.Internet.Password())
        .RuleFor(x => x.Email, f => f.Internet.Email())
        .RuleFor(x => x.IsEnabled, _ => true)
        .RuleFor(x => x.IsValidated, _ => true)
        .RuleFor(x => x.ValidatedAt, f => f.Date.Recent())
        .RuleFor(x => x.PlexId, _ => GetUniqueNumber())
        .RuleFor(x => x.Uuid, f => f.Random.Guid().ToString())
        .RuleFor(x => x.ClientId, f => f.Random.Guid().ToString())
        .RuleFor(x => x.Title, f => f.Internet.UserName())
        .RuleFor(x => x.HasPassword, _ => true)
        .RuleFor(x => x.AuthenticationToken, f => f.Random.Guid().ToString())
        .Ignore(x => x.CustomAuthenticationToken)
        .RuleFor(x => x.IsMain, _ => true)
        .RuleFor(x => x.Is2Fa, _ => false)
        .RuleFor(x => x.IsAuthTokenMode, _ => false)
        .Ignore(x => x.VerificationCode)
        .Ignore(x => x.PlexAccountServers)
        .Ignore(x => x.PlexAccountLibraries);

    public static Faker<PlexAccount> GetPlexAccount(int seed) => GetPlexAccount(new Seed(seed));

    public static Faker<PlexAccount> GetPlexAccount(Seed seed) => _plexAccountFaker.UseSeed(seed.Next());
}
