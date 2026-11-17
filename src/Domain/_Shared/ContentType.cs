using System.Net.Http.Headers;

namespace Reaparr.Domain;

public static class ContentType
{
    private static readonly HashSet<string> _passiveImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/apng",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/tiff",
        "image/heic",
        "image/heif",
        "image/avif",
        "image/jxl",
    };

    public static bool IsPassiveImage(string? mediaType) =>
        mediaType is not null && _passiveImageTypes.Contains(mediaType);

    public static string ApplicationJson => "application/json";

    public static string TextHtml => "text/html";

    public static MediaTypeWithQualityHeaderValue ApplicationJsonHeaderValue => new(ApplicationJson);
}
