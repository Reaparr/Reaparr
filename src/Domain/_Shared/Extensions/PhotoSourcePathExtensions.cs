using System.Diagnostics.CodeAnalysis;

namespace Reaparr.Domain;

/// <summary>
/// Normalizes Plex source strings independently of the host OS. Destination paths use the filesystem abstraction.
/// </summary>
public static class PhotoSourcePathExtensions
{
    public static bool TryCreatePhotoRootIdentity(
        [NotNullWhen(true)] this string? path,
        out string identityKey,
        out string canonicalPath,
        out string error
    )
    {
        identityKey = canonicalPath = error = string.Empty;
        if (!TryNormalize(path, out var normalized, out var windows))
        {
            error = "Photo roots must be absolute POSIX, Windows drive or UNC paths without traversal components.";
            return false;
        }

        canonicalPath = windows ? normalized.ToUpperInvariant() : normalized;
        identityKey = $"path:{(windows ? "windows" : "posix")}:{canonicalPath}";
        return true;
    }

    /// <summary>
    /// Matches roots already normalized at section ingestion. Missing mappings remain non-downloadable catalog files.
    /// </summary>
    public static bool TryResolvePhotoSourcePath(
        this string sourcePath,
        IReadOnlyCollection<PlexPhotoFolder> roots,
        out (PlexPhotoFolder Root, string RelativePath, string CanonicalRelativePath) match
    )
    {
        match = default;
        if (!TryNormalize(sourcePath, out var normalized, out var windows))
            return false;

        var comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        PlexPhotoFolder? selected = null;
        var ambiguous = false;
        foreach (var root in roots)
        {
            var rootPath = root.SourceRootPath!;
            if (IsWindowsPath(rootPath) != windows || !IsWithinRoot(normalized, rootPath, comparison))
                continue;
            if (selected is null || rootPath.Length > selected.SourceRootPath!.Length)
            {
                selected = root;
                ambiguous = false;
            }
            else if (
                rootPath.Length == selected.SourceRootPath!.Length
                && root.RootIdentityKey != selected.RootIdentityKey
            )
                ambiguous = true;
        }

        if (ambiguous)
            throw new InvalidOperationException("Photo source path matches equally specific configured roots.");
        if (selected is null)
            return false;

        var relativeStart = selected.SourceRootPath!.Length;
        if (!selected.SourceRootPath.EndsWith('/'))
            relativeStart++;
        var relativePath = normalized[relativeStart..];
        match = (selected, relativePath, windows ? relativePath.ToUpperInvariant() : relativePath);
        return true;
    }

    public static string GetSourcePathLeafName(this string? path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;
        var separator = IsWindowsPath(path)
            ? Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'))
            : path.LastIndexOf('/');
        return path[(separator + 1)..];
    }

    private static bool IsWithinRoot(string source, string root, StringComparison comparison) =>
        source.Length > root.Length
        && source.StartsWith(root, comparison)
        && (root.EndsWith('/') || source[root.Length] == '/');

    private static bool IsWindowsPath(string path) =>
        path.StartsWith("\\\\", StringComparison.Ordinal)
        || path.StartsWith("//", StringComparison.Ordinal)
        || (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\');

    private static bool TryNormalize([NotNullWhen(true)] string? path, out string normalized, out bool windows)
    {
        normalized = string.Empty;
        windows = false;
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0'))
            return false;
        windows = IsWindowsPath(path);
        if (!windows && !path.StartsWith('/'))
            return false;

        normalized = windows ? path.Replace('\\', '/') : path;
        var unc = normalized.StartsWith("//", StringComparison.Ordinal);
        if (normalized.AsSpan(unc ? 2 : 0).IndexOf("//") >= 0)
        {
            string prefix;
            if (unc)
                prefix = "//";
            else if (windows)
                prefix = string.Empty;
            else
                prefix = "/";
            normalized = prefix + string.Join('/', normalized.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }

        var minimumLength = windows && !unc ? 3 : 1;
        if (normalized.Length > minimumLength && normalized.EndsWith('/'))
            normalized = normalized.TrimEnd('/');
        if (windows && !unc && normalized.Length == 2)
            normalized += "/";
        if (unc && normalized.IndexOf('/', 2) < 0)
            return false;

        foreach (var range in normalized.AsSpan().Split('/'))
        {
            var segment = normalized.AsSpan()[range];
            if (segment.SequenceEqual(".") || segment.SequenceEqual(".."))
                return false;
        }
        return true;
    }
}
