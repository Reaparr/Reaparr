using Reaparr.PlexApi.Contracts;

namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexServer> _plexServerFaker = new Faker<PlexServer>()
        .StrictMode(true)
        .Ignore(x => x.Id)
        .RuleFor(x => x.Name, f => f.Company.CompanyName())
        .RuleFor(x => x.Product, _ => "Plex Media Server")
        .RuleFor(x => x.ProductVersion, f => f.System.Semver())
        .RuleFor(x => x.Platform, _ => "Linux")
        .RuleFor(x => x.PlatformVersion, f => f.System.Semver())
        .RuleFor(x => x.Device, f => f.Company.CompanyName())
        .RuleFor(x => x.MachineIdentifier, f => f.Random.Guid().ToString())
        .RuleFor(x => x.CreatedAt, f => f.Date.Past(10, DateTime.UtcNow))
        .RuleFor(x => x.LastSeenAt, f => f.Date.Recent(30))
        .RuleFor(x => x.Provides, f => f.Company.CompanyName())
        .RuleFor(x => x.OwnerId, f => f.Random.Int(1000, 100000))
        .RuleFor(x => x.PlexServerOwnerUsername, f => f.Name.LastName())
        .RuleFor(x => x.PublicAddress, f => f.Internet.Ip())
        .RuleFor(x => x.IsEnabled, _ => true)
        .RuleFor(x => x.IsDownloadsPausedByUser, _ => false)
        // Server flags
        .RuleFor(x => x.Synced, f => f.Random.Bool())
        .RuleFor(x => x.Relay, f => f.Random.Bool())
        .RuleFor(x => x.Presence, f => f.Random.Bool())
        .RuleFor(x => x.HttpsRequired, f => f.Random.Bool())
        .RuleFor(x => x.PublicAddressMatches, f => f.Random.Bool())
        .RuleFor(x => x.DnsRebindingProtection, f => f.Random.Bool())
        .RuleFor(x => x.NatLoopbackSupported, f => f.Random.Bool())
        .Ignore(x => x.OwnedOverride)
        .Ignore(x => x.PreferredConnectionId)
        .Ignore(x => x.PlexServerConnections)
        .Ignore(x => x.PlexLibraries)
        .Ignore(x => x.ServerStatus)
        .Ignore(x => x.PlexAccountServers);

    public static Faker<PlexServer> GetPlexServer(Seed seed, Action<FakeDataConfig>? options = null) =>
        // Note: Ensure all faker values are a lambda f => x,
        // otherwise Entity Framework will see differently generated values as the same object and mess up any database testing
        _plexServerFaker.UseSeed(seed.Next());

    private static readonly Faker<FolderPath> _folderPathFaker = new Faker<FolderPath>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => 0)
        .RuleFor(x => x.DisplayName, f => f.Random.Word())
        .RuleFor(x => x.FolderType, f => f.Random.Enum<FolderType>())
        .RuleFor(x => x.MediaType, f => f.Random.Enum<PlexMediaType>())
        .RuleFor(x => x.DirectoryPath, f => f.System.DirectoryPath())
        .Ignore(x => x.PlexLibraries);

    public static Faker<FolderPath> GetFolderPaths(Seed seed)
    {
        var ids = 0;
        return _folderPathFaker.RuleFor(x => x.Id, _ => ids++).UseSeed(seed.Next());
    }

    private static readonly Faker<PlexServerConnection> _plexServerConnectionFaker = new Faker<PlexServerConnection>()
        .StrictMode(true)
        .Ignore(x => x.Id)
        .RuleFor(x => x.Protocol, f => f.Internet.Protocol())
        .RuleFor(x => x.Address, f => f.Internet.Ip())
        .RuleFor(x => x.Port, f => f.Internet.Port())
        .RuleFor(x => x.Local, _ => false)
        .RuleFor(x => x.Relay, _ => false)
        .RuleFor(x => x.IPv4, _ => true)
        .RuleFor(x => x.IPv6, _ => false)
        .RuleFor(x => x.IsCustom, _ => false)
        .RuleFor(x => x.Url, (_, x) => $"{x.Protocol}://{x.Address}:{x.Port}")
        .Ignore(x => x.LatestConnectionStatus)
        .Ignore(x => x.PlexServer)
        .RuleFor(x => x.PlexServerId, _ => 0);

    public static Faker<PlexServerConnection> GetPlexServerConnections(
        Seed seed,
        Action<FakeDataConfig>? options = null,
        bool isCustom = false,
        int plexServerId = 0
    )
    {
        return _plexServerConnectionFaker
            .RuleFor(x => x.IsCustom, _ => isCustom)
            .RuleFor(x => x.PlexServerId, _ => plexServerId)
            .UseSeed(seed.Next());
    }

    public static Faker<PlexServerStatus> GetPlexServerStatus(
        Seed seed,
        bool isSuccessful = true,
        int plexServerId = 0,
        int plexServerConnectionId = 0
    )
    {
        return new Faker<PlexServerStatus>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .Ignore(x => x.Id)
            .RuleFor(x => x.IsSuccessful, _ => isSuccessful)
            .RuleFor(x => x.StatusCode, _ => 200)
            .RuleFor(x => x.StatusMessage, f => f.Hacker.Phrase())
            .RuleFor(x => x.LastChecked, f => f.Date.Recent())
            .Ignore(x => x.PlexServerConnection)
            .RuleFor(x => x.PlexServerConnectionId, _ => plexServerConnectionId)
            .Ignore(x => x.PlexServer)
            .RuleFor(x => x.PlexServerId, _ => plexServerId);
    }

    private static readonly Faker<PlexAccountServer> _plexAccountServerFaker = new Faker<PlexAccountServer>()
        .StrictMode(true)
        .RuleFor(x => x.PlexAccountId, _ => 0)
        .Ignore(x => x.PlexAccount)
        .RuleFor(x => x.PlexServerId, _ => 0)
        .Ignore(x => x.PlexServer)
        .RuleFor(x => x.AuthToken, f => f.Random.Uuid().ToString())
        .RuleFor(x => x.AuthTokenCreationDate, _ => DateTime.UtcNow)
        .RuleFor(x => x.IsServerOwned, f => f.Random.Bool());

    public static List<PlexAccountServer> GetPlexAccountServer(
        Seed seed,
        PlexAccount plexAccount,
        List<PlexServer> plexServers
    )
    {
        var index = 0;
        return _plexAccountServerFaker
            .RuleFor(x => x.PlexAccountId, _ => plexAccount.Id)
            .RuleFor(x => x.PlexServerId, _ => plexServers[index++].Id)
            .UseSeed(seed.Next())
            .Generate(plexServers.Count);
    }

    private static readonly Faker<ServerAccessTokenDTO> _serverAccessTokenDtoFaker = new Faker<ServerAccessTokenDTO>()
        .StrictMode(true)
        .RuleFor(x => x.PlexAccountId, _ => 0)
        .RuleFor(x => x.MachineIdentifier, _ => string.Empty)
        .RuleFor(x => x.AccessToken, f => f.Random.Uuid().ToString())
        .RuleFor(x => x.IsServerOwned, f => f.Random.Bool());

    public static List<ServerAccessTokenDTO> GetServerAccessTokenDTO(
        Seed seed,
        PlexAccount plexAccount,
        List<PlexServer> plexServers
    )
    {
        var index = 0;
        return _serverAccessTokenDtoFaker
            .RuleFor(x => x.PlexAccountId, _ => plexAccount.Id)
            .RuleFor(x => x.MachineIdentifier, _ => plexServers[index++].MachineIdentifier)
            .UseSeed(seed.Next())
            .Generate(plexServers.Count);
    }
}
