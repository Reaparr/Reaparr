namespace Reaparr.PublicAPI;

public static class TorznabResponseSenderExtensions
{
    public static Task TorznabError(
        this IResponseSender sender,
        int code,
        string description,
        CancellationToken ct = default
    ) =>
        sender.XmlAsync(
            new TorznabErrorResponseDTO { Code = code, Description = description },
            cancellationToken: ct
        );
}
