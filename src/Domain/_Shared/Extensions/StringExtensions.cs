using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PasswordGenerator;

namespace Reaparr.Domain;

public static partial class StringExtensions
{
    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an",
        "and",
        "the",
        "of",
        "in",
        "on",
        "for",
        "with",
        "to",
        "by",
        "at",
    };

    private static readonly string[] _articles = ["a", "an", "the", "and"];

    public static string GetActualCasing(this string path)
    {
        if (!OperatingSystem.IsWindows() || path.StartsWith("\\"))
            return path;

        if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
            return GetProperCapitalization(new DirectoryInfo(path));

        var fileInfo = new FileInfo(path);
        var dirInfo = fileInfo.Directory;

        var fileName = fileInfo.Name;

        if (dirInfo != null && fileInfo.Exists)
            fileName = dirInfo.GetFiles(fileInfo.Name)[0].Name;

        return Path.Combine(GetProperCapitalization(dirInfo), fileName);
    }

    public static string CapitalizeFirst(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (text.Length == 1)
            return text.ToUpperInvariant();

        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    public static string GeneratePassword(
        int passwordLength = 16,
        bool includeLowercase = true,
        bool includeUppercase = true,
        bool includeNumeric = true,
        bool includeSpecial = true
    ) =>
        new Password(
            includeLowercase: includeLowercase,
            includeUppercase: includeUppercase,
            includeNumeric: includeNumeric,
            includeSpecial: includeSpecial,
            passwordLength: passwordLength
        ).Next();

    public static bool IsIpAddress(this string ipAddress) => IPAddress.TryParse(ipAddress, out var _);

    public static bool IsValidHttpUrl(this string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string GetProperCapitalization(DirectoryInfo? dirInfo)
    {
        if (dirInfo == null)
            return string.Empty;

        var parentDirInfo = dirInfo.Parent;
        if (parentDirInfo == null)
        {
            // Drive letter
            return dirInfo.Name.ToUpper();
        }

        var folderName = dirInfo.Name;

        if (dirInfo.Exists)
            folderName = parentDirInfo.GetDirectories(dirInfo.Name)[0].Name;

        return Path.Combine(GetProperCapitalization(parentDirInfo), folderName);
    }

    /// <summary>
    /// Replaces invalid characters from a file or folder name
    /// Source: https://stackoverflow.com/a/13617375/8205497
    /// </summary>
    /// <param name="name"> The filename or folder name to sanitize. </param>
    /// <returns> The sanitized filename or folder name. </returns>
    public static string SanitizeFolderName(this string name)
    {
        var invalids = Path.GetInvalidFileNameChars();
        name = name.Replace(@"·", "-").Replace(": ", " ");
        return string.Join(" ", name.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    /// <summary>
    /// Converts a title to a sort title that can be used for sorting.
    /// </summary>
    /// <param name="title"></param>
    /// <returns></returns>
    public static string ToSortTitle(this string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Convert to lowercase
        var lowerTitle = title.ToLowerInvariant();

        // Remove leading articles
        var sortTitle = RemoveLeadingArticle(lowerTitle);

        // Remove punctuation and diacritics
        sortTitle = RemoveDiacritics(sortTitle);
        sortTitle = RemovePunctuationRegex().Replace(sortTitle, "");

        // Trim any leading or trailing whitespace
        return sortTitle.Trim();
    }

    public static string ToSearchTitle(this string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Convert to lower case
        var lowerTitle = title.ToLowerInvariant();

        // Remove punctuation and diacritics
        var normalizedTitle = RemoveDiacritics(lowerTitle);
        normalizedTitle = RemovePunctuationRegex().Replace(normalizedTitle, "");

        // Split title into words and remove stop words
        var words = normalizedTitle
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !_stopWords.Contains(word));

        // Join the words back into a string
        var searchTitle = string.Join(" ", words);

        return searchTitle;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                stringBuilder.Append(c);
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string RemoveLeadingArticle(string title)
    {
        foreach (var article in _articles)
        {
            var articleWithSpace = article + " ";
            if (title.StartsWith(articleWithSpace, StringComparison.OrdinalIgnoreCase))
                return title.Substring(articleWithSpace.Length);
        }

        return title;
    }

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex RemovePunctuationRegex();

    public static string GetFileName(this string path)
    {
        path = path.Replace(@"\", "/");
        return Path.GetFileName(path);
    }

    public static StringContent ToStringContent(this string json) =>
        new(json, Encoding.UTF8, ContentType.ApplicationJson);

    public static bool IsLocalUrl(this string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false; // Invalid URL
        }

        // Check if the hostname is "localhost" or "127.0.0.1" (IPv4 loopback)
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("127.0.0.1"))
        {
            return true;
        }

        // Check if it's an IP address
        if (IPAddress.TryParse(uri.Host, out var ipAddress))
        {
            // Check if it's an IPv4 private or loopback address
            if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = ipAddress.GetAddressBytes();
                return bytes[0] == 10
                    || // 10.x.x.x
                    (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || // 172.16.x.x - 172.31.x.x
                    (bytes[0] == 192 && bytes[1] == 168)
                    || // 192.168.x.x
                    ipAddress.Equals(IPAddress.Loopback); // 127.0.0.1
            }

            // Check if it's an IPv6 local or loopback address
            if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ipAddress.IsIPv6LinkLocal
                    || // fe80::/10 range
                    ipAddress.IsIPv6SiteLocal
                    || // fec0::/10 range (deprecated but sometimes still used)
                    ipAddress.Equals(IPAddress.IPv6Loopback); // ::1
            }
        }

        return false;
    }

    public static bool IsIpv4(this string address)
    {
        if (IPAddress.TryParse(address, out var ipAddress))
        {
            return ipAddress.AddressFamily == AddressFamily.InterNetwork;
        }

        return false;
    }

    public static bool IsIPv6(this string address)
    {
        if (IPAddress.TryParse(address, out var ipAddress))
        {
            return ipAddress.AddressFamily == AddressFamily.InterNetworkV6;
        }

        return false;
    }

    public static string ToListString<T>(this List<T> list) => !list.Any() ? "[ - ]" : $"[{string.Join(", ", list)}]";

    /// <summary>
    /// Returns the lowercase hexadecimal MD5 hash of the input string.
    /// Always the same for the same input.
    /// </summary>
    public static string ToMd5Hash(this string? input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        var normalized = input.Trim().ToLowerInvariant();
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(normalized));

        // Allocate a char array directly for hex conversion
        var chars = new char[hash.Length * 2];
        for (var i = 0; i < hash.Length; i++)
        {
            var b = hash[i];
            chars[i * 2] = GetHexChar(b >> 4);
            chars[i * 2 + 1] = GetHexChar(b & 0xF);
        }

        return new string(chars);

        static char GetHexChar(int val) => (char)(val < 10 ? '0' + val : 'a' + (val - 10));
    }

    public static DateTime? ToDateTime(this string source)
    {
        if (DateTime.TryParse(source, out var dateTimeResult))
            return dateTimeResult;

        return null;
    }
}
