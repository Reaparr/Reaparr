using Flurl;

namespace Reaparr.Settings.Contracts;

public record NetworkSettingsModule
    : BaseSettingsModule<NetworkSettingsModule>,
        IBaseSettingsModule<NetworkSettingsModule>,
        INetworkSettings
{
    public static NetworkSettingsModule Create() =>
        new()
        {
            ReverseProxyUrl = string.Empty,
            BasePath = string.Empty,
            TrustProxyHeaders = false,
            AllowedProxyIps = [],
            ForwardedHostHeader = string.Empty,
            ForwardedPathHeader = string.Empty,
        };

    public required string ReverseProxyUrl
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public required string BasePath
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public required bool TrustProxyHeaders
    {
        get;
        set => SetProperty(ref field, value);
    }

    public required List<string> AllowedProxyIps
    {
        get;
        set => SetProperty(ref field, value);
    } = [];

    public required string ForwardedHostHeader
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public required string ForwardedPathHeader
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    [JsonIgnore]
    public Uri Uri => new(Url);

    [JsonIgnore]
    public string Url
    {
        get
        {
            // We explicitly convert to Uri and then to AbsoluteUri to ensure
            // this property always returns a pure string.
            //
            // Flurl defines implicit conversions between string and Url.
            // If we return a Flurl.Url (even indirectly), serializers may treat
            // it as an object and serialize its internal properties (scheme, host, etc.)
            // instead of a simple URL string.
            //
            // By converting to Uri and using AbsoluteUri, we guarantee:
            //  - No Flurl.Url instance leaks outside this property
            //  - No implicit operator ambiguity
            //  - Safe and predictable JSON serialization
            if (
                !string.IsNullOrWhiteSpace(ReverseProxyUrl)
                && Uri.TryCreate(ReverseProxyUrl, UriKind.Absolute, out var reverseProxyUri)
            )
            {
                return new Url(reverseProxyUri).AppendPathSegment(BasePath).ToUri().AbsoluteUri;
            }

            return new UriBuilder
            {
                Scheme = "http",
                Host = "localhost",
                Port = 5000,
            }
                .Uri
                .AbsoluteUri;
        }
    }
}
