namespace Reaparr.Application.Contracts;

public static class HttpRequestMessageRetryProgressExtensions
{
    public static void SetRetryProgressCallback(
        this HttpRequestMessage request,
        Action<HttpRequestRetryProgress>? callback
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (callback is null)
            return;

        request.Options.Set(new("RetryProgressCallbackKey"), callback);
    }

    public static Action<HttpRequestRetryProgress>? GetRetryProgressCallback(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Options.TryGetValue(
            (HttpRequestOptionsKey<Action<HttpRequestRetryProgress>>)new("RetryProgressCallbackKey"),
            out var callback
        )
            ? callback
            : null;
    }

    public static void SetRetryCount(this HttpRequestMessage request, int retryCount)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Options.Set(new("RetryCountKey"), retryCount);
    }

    public static int? GetRetryCount(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Options.TryGetValue(new("RetryCountKey"), out int retryCount) ? retryCount : null;
    }

    public static void SetResponseBufferingTimeout(this HttpRequestMessage request, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Options.Set(new("ResponseBufferingTimeoutKey"), timeout);
    }

    public static TimeSpan? GetResponseBufferingTimeout(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Options.TryGetValue(
            new HttpRequestOptionsKey<TimeSpan>("ResponseBufferingTimeoutKey"),
            out var timeout
        )
            ? timeout
            : null;
    }
}
