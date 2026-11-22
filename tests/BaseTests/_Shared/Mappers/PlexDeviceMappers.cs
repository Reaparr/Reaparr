using LukeHagar.PlexAPI.SDK.Models.Components;

namespace Reaparr.BaseTests;

public static class PlexDeviceMappers
{
    public static PlexDevice ToPlexApiDTO(this PlexServer source) =>
        new()
        {
            Name = source.Name,
            Product = source.Product,
            ProductVersion = source.ProductVersion,
            Platform = source.Platform,
            PlatformVersion = source.PlatformVersion,
            Device = source.Device,
            ClientIdentifier = source.MachineIdentifier,
            CreatedAt = source.CreatedAt,
            LastSeenAt = source.LastSeenAt,
            Provides = source.Provides,
            OwnerId = source.OwnerId,
            SourceTitle = source.PlexServerOwnerUsername,
            PublicAddress = source.PublicAddress,
            AccessToken = "FakeAccessToken",
            Owned = source.Owned,
            Synced = source.Synced,
            Relay = source.Relay,
            Presence = source.Presence,
            HttpsRequired = source.HttpsRequired,
            PublicAddressMatches = source.PublicAddressMatches,
            DnsRebindingProtection = source.DnsRebindingProtection,
            NatLoopbackSupported = source.NatLoopbackSupported,
            Connections = source.PlexServerConnections.Select(y => y.ToPlexApiDTO()).ToList(),
        };

    public static Connections ToPlexApiDTO(this PlexServerConnection connection) =>
        new()
        {
            Protocol = connection.Protocol.Equals("https", StringComparison.OrdinalIgnoreCase)
                ? PlexDeviceProtocol.Https
                : PlexDeviceProtocol.Http,
            Address = connection.Address,
            Port = connection.Port,
            Uri = connection.Url,
            Local = connection.Local,
            Relay = connection.Relay,
            IPv6 = connection.IPv6,
        };

    public static List<PlexDevice> ToPlexApiDTO(this List<PlexServer> plexServers) =>
        plexServers.Select(x => x.ToPlexApiDTO()).ToList();
}
