using System.Net;
using System.Security.Claims;
using Reaparr.Identity.Contracts;
using Reaparr.Settings.Contracts;

namespace Reaparr.AppHost;

/// <summary>
/// Middleware for handling header-based authentication
/// </summary>
public class HeaderAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAppRuntimeInfo _appRuntimeInfo;
    private readonly IIdentitySignInService _signInService;
    private readonly Serilog.ILogger _log;
    private readonly IHeaderAuthenticationSettings _headerAuthentication;
    private readonly IUserService _userService;

    /// <summary>
    /// Constructor for HeaderAuthenticationMiddleware
    /// </summary>
    public HeaderAuthenticationMiddleware(
        RequestDelegate next,
        Serilog.ILogger log,
        IAppRuntimeInfo appRuntimeInfo,
        IIdentitySignInService signInService,
        IAuthenticationSettings authenticationSettings,
        IUserService userService
    )
    {
        _log = log.ForContext<HeaderAuthenticationMiddleware>();
        _next = next;
        _appRuntimeInfo = appRuntimeInfo;
        _signInService = signInService;
        _headerAuthentication = authenticationSettings.HeaderAuthentication;
        _userService = userService;
    }

    /// <summary>
    /// Header-based authentication middleware invocation
    /// </summary>
    /// <param name="context"></param>
    public async Task InvokeAsync(HttpContext context)
    {
        var headerValue = ExtractHeaderValue(context);

        // Skip early if header not present
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            await _next(context);
            return;
        }

        // Skip if header authentication is disabled
        if (!_headerAuthentication.Enabled)
        {
            if (_headerAuthentication.EnableLogging)
                _log.Here()
                    .Warning(
                        "Header authentication is disabled but the header: {HeaderName} was present. Enable header authentication first in the settings file",
                        _appRuntimeInfo.HeaderAuthTokenName
                    );
            await _next(context);
            return;
        }

        // Skip if the user is already authenticated
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (_headerAuthentication.EnableLogging)
                _log.Here().Debug("User is already authenticated");

            await _next(context);
            return;
        }

        // Check if request comes from trusted proxy
        if (!IsRequestFromTrustedProxy(context))
        {
            if (_headerAuthentication.EnableLogging)
                _log.Here()
                    .Warning(
                        "Request with header authentication token from untrusted IP: {RemoteIp}",
                        context.Connection.RemoteIpAddress
                    );
            await _next(context);
            return;
        }

        // Check HTTPS requirement
        if (_headerAuthentication.RequireHttps && !context.Request.IsHttps)
        {
            if (_headerAuthentication.EnableLogging)
                _log.Here()
                    .Warning(
                        "Header authentication requires HTTPS but request is not secure. IP: {RemoteIp}",
                        context.Connection.RemoteIpAddress
                    );

            await _next(context);
            return;
        }

        // Validate header value length
        if (headerValue.Length > _headerAuthentication.MaxHeaderLength)
        {
            if (_headerAuthentication.EnableLogging)
                _log.Here()
                    .Warning(
                        "Header value exceeds maximum length. Length: {Length}, Max: {MaxLength}, IP: {RemoteIp}",
                        headerValue.Length,
                        _headerAuthentication.MaxHeaderLength,
                        context.Connection.RemoteIpAddress
                    );

            await _next(context);
            return;
        }

        // Map header value to user
        var user = await MapHeaderToUser(headerValue);
        if (user == null)
        {
            if (_headerAuthentication.EnableLogging)
                _log.Here()
                    .Warning(
                        "The wrong user is passed in. Make sure to use the same username you use to log into Reaparr. Header: {HeaderName}, Value: {HeaderValue}, IP: {RemoteIp}",
                        _appRuntimeInfo.HeaderAuthTokenName,
                        headerValue,
                        context.Connection.RemoteIpAddress
                    );

            await _next(context);
            return;
        }

        var claims = new[] { new Claim("HeaderAuth", "true") };
        await _signInService.SignInAsync(user, claims);

        if (_headerAuthentication.EnableLogging)
            _log.Here()
                .Debug(
                    "User authenticated via header. User: {UserName}, IP: {RemoteIp}, Header: {HeaderName}",
                    user.UserName,
                    context.Connection.RemoteIpAddress,
                    _appRuntimeInfo.HeaderAuthTokenName
                );

        await _next(context);
    }

    private bool IsRequestFromTrustedProxy(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null)
            return false;

        if (remoteIp.IsIPv4MappedToIPv6)
            remoteIp = remoteIp.MapToIPv4();

        // If no proxies are configured, deny everyone (safe default)
        if (!_headerAuthentication.TrustedProxies.Any())
            return false;

        return _headerAuthentication.TrustedProxies.Any(proxy => IsIpInRange(remoteIp, proxy));
    }

    private static bool IsIpInRange(IPAddress ip, string cidrOrIp)
    {
        if (string.IsNullOrWhiteSpace(cidrOrIp))
            return false;

        if (cidrOrIp.Contains('/'))
        {
            // CIDR notation
            var parts = cidrOrIp.Split('/');
            if (parts.Length != 2)
                return false;

            if (!IPAddress.TryParse(parts[0], out var networkIp) || !int.TryParse(parts[1], out var prefixLength))
                return false;

            try
            {
                var mask = CreateSubnetMask(
                    prefixLength,
                    networkIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 4 : 6
                );
                return IsIpInSubnet(ip, networkIp, mask);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false; // Invalid prefix length
            }
        }

        // Single IP address
        return IPAddress.TryParse(cidrOrIp, out var singleIp) && ip.Equals(singleIp);
    }

    private static IPAddress CreateSubnetMask(int prefixLength, int addressFamily)
    {
        if (addressFamily == 4) // IPv4
        {
            if (prefixLength < 0 || prefixLength > 32)
                throw new ArgumentOutOfRangeException(
                    nameof(prefixLength),
                    "IPv4 prefix length must be between 0 and 32"
                );

            if (prefixLength == 0)
            {
                return new IPAddress(new byte[] { 0, 0, 0, 0 });
            }

            var mask = 0xFFFFFFFF << (32 - prefixLength);
            var maskBytes = BitConverter.GetBytes(mask);
            Array.Reverse(maskBytes);
            return new IPAddress(maskBytes);
        }
        // IPv6
        {
            if (prefixLength < 0 || prefixLength > 128)
                throw new ArgumentOutOfRangeException(
                    nameof(prefixLength),
                    "IPv6 prefix length must be between 0 and 128"
                );

            var mask = new byte[16];
            var fullBytes = prefixLength / 8;
            var remainingBits = prefixLength % 8;

            for (var i = 0; i < fullBytes; i++)
            {
                mask[i] = 0xFF;
            }

            if (remainingBits > 0)
            {
                mask[fullBytes] = (byte)(0xFF << (8 - remainingBits));
            }

            return new IPAddress(mask);
        }
    }

    private static bool IsIpInSubnet(IPAddress ip, IPAddress network, IPAddress mask)
    {
        var ipBytes = ip.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();

        // IP addresses must have the same length to be comparable
        if (ipBytes.Length != networkBytes.Length || ipBytes.Length != maskBytes.Length)
            return false;

        for (var i = 0; i < ipBytes.Length; i++)
        {
            if ((ipBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
                return false;
        }

        return true;
    }

    private string? ExtractHeaderValue(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(_appRuntimeInfo.HeaderAuthTokenName, out var headerValues))
            return null;

        var headerValue = headerValues.FirstOrDefault();
        return string.IsNullOrWhiteSpace(headerValue) ? null : headerValue.Trim();
    }

    private async Task<AppUser?> MapHeaderToUser(string headerValue)
    {
        return _headerAuthentication.MappingType switch
        {
            HeaderMappingType.Username => await _userService.FindByNameAsync(headerValue),
            HeaderMappingType.Email => await _userService.FindByEmailAsync(headerValue),
            _ => null,
        };
    }
}
