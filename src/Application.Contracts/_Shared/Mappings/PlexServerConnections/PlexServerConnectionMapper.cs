namespace Reaparr.Application.Contracts;

public static class PlexServerConnectionMapper
{
    public static PlexServerConnectionDTO ToDTO(this PlexServerConnection source) =>
        new()
        {
            Id = source.Id,
            Protocol = source.Protocol,
            Address = source.Address,
            Port = source.Port,
            Local = source.Local,
            Relay = source.Relay,
            IPv4 = source.IPv4,
            IPv6 = source.IPv6,
            Url = source.Url,
            PlexServerId = source.PlexServerId,
            IsCustom = source.IsCustom,
            IsPlexTvConnection = source.IsPlexTvConnection,
            LatestConnectionStatus = source.LatestConnectionStatus?.ToDTO(),
            Type = source.Type,
            ChosenConnection = false,
        };

    public static List<PlexServerConnectionDTO> ToDTO(this List<PlexServerConnection> source) =>
        source.ConvertAll(ToDTO);
}
