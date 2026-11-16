using System.Security.Cryptography;

namespace Reaparr.Application.Contracts;

public static class IntegrationApiKeyGenerator
{
    private const string Q_BITTORRENT_ALPHABET = "23456789ABCDEFGHIJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";

    public static string GenerateQBittorrentApiKey() =>
        $"qbt_{RandomNumberGenerator.GetString(Q_BITTORRENT_ALPHABET, 28)}";

    public static string GenerateQBittorrentApiKey(Guid integrationId)
    {
        var hash = SHA256.HashData(integrationId.ToByteArray());
        var key = string.Create(28, hash, static (characters, bytes) =>
        {
            for (var i = 0; i < characters.Length; i++)
                characters[i] = Q_BITTORRENT_ALPHABET[bytes[i % bytes.Length] % Q_BITTORRENT_ALPHABET.Length];
        });
        return $"qbt_{key}";
    }

    public static string GenerateTorznabApiKey() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    public static string GenerateTorznabApiKey(Guid integrationId) =>
        Convert.ToHexStringLower(SHA256.HashData(integrationId.ToByteArray()))[..32];
}
