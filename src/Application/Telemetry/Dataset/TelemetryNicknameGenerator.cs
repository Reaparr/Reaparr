using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Reaparr.Application;

/// <summary>Derives a stable display nickname from a UUID without replacing its identity.</summary>
internal static class TelemetryNicknameGenerator
{
    internal static string Generate(string identity)
    {
        // Cached ordinal views make the hash-set datasets independent of enumeration order.
        var adjectives = TelemetryNicknameDataset.OrderedAdjectives.Value;
        var nouns = TelemetryNicknameDataset.OrderedNouns.Value;
        var species = TelemetryNicknameDataset.OrderedSpecies.Value;
        var canonicalId = Guid.Parse(identity).ToString("D");
        Span<byte> canonicalBytes = stackalloc byte[36];
        Encoding.ASCII.GetBytes(canonicalId, canonicalBytes);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(canonicalBytes, digest);
        var speciesIndex = BinaryPrimitives.ReadUInt32BigEndian(digest[2..6]) % (uint)species.Length;
        return $"{adjectives[digest[0] % adjectives.Length]} {nouns[digest[1] % nouns.Length]} {species[speciesIndex]} · {canonicalId[..8]}";
    }
}
