using System.Collections.Concurrent;
using System.Net;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Reaparr.Application;

internal sealed class DefaultHttpClientRetryHandler(ILogger log) : DelegatingHandler
{
    private const int MAX_RETRY_ATTEMPTS = 3;

    private static readonly ConcurrentDictionary<
        (int RetryCount, TimeSpan? BufferTimeout),
        ResiliencePipeline<HttpResponseMessage>
    > _pipelines = new();

    private static readonly ResiliencePropertyKey<Action<HttpRequestRetryProgress>?> _progressCallbackKey = new(
        "RetryProgressCallback"
    );
    private static readonly ResiliencePropertyKey<string> _requestUriKey = new("RequestUri");
    private static readonly ResiliencePropertyKey<string> _requestMethodKey = new("RequestMethod");
    private static readonly ResiliencePropertyKey<int> _retryCountKey = new("RetryCount");
    private static readonly ResiliencePropertyKey<bool> _bufferingEnabledKey = new("BufferingEnabled");
    private static readonly ResiliencePropertyKey<int> _latestRetryAttemptKey = new("LatestRetryAttempt");
    private static readonly ResiliencePropertyKey<ILogger?> _loggerKey = new("Logger");

    private readonly ILogger _log = log.ForContext<DefaultHttpClientRetryHandler>();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var requestUri = request.RequestUri?.ToString() ?? "unknown";
        var retryCount = Math.Max(request.GetRetryCount() ?? MAX_RETRY_ATTEMPTS, 0);
        var bufferTimeout = request.GetResponseBufferingTimeout();
        var progressCallback = request.GetRetryProgressCallback();
        var pipeline = _pipelines.GetOrAdd((retryCount, bufferTimeout), static key =>
            CreateRetryPipeline(key.RetryCount, key.BufferTimeout)
        );
        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(_progressCallbackKey, progressCallback);
        context.Properties.Set(_requestUriKey, requestUri);
        context.Properties.Set(_requestMethodKey, request.Method.Method);
        context.Properties.Set(_retryCountKey, retryCount);
        context.Properties.Set(_bufferingEnabledKey, bufferTimeout is not null);
        context.Properties.Set(_latestRetryAttemptKey, 0);
        context.Properties.Set(_loggerKey, _log);

        try
        {
            var response = await pipeline
                .ExecuteAsync(
                    static (executionContext, state) => ExecuteAttemptAsync(executionContext, state),
                    context,
                    (Handler: this, Request: request)
                )
                .ConfigureAwait(false);

            progressCallback?.Invoke(
                CreateCompletedProgress(
                    response,
                    requestUri,
                    context.Properties.GetValue(_latestRetryAttemptKey, 0),
                    retryCount
                )
            );
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke(
                CreateCompletedProgress(
                    ex,
                    requestUri,
                    context.Properties.GetValue(_latestRetryAttemptKey, 0),
                    retryCount
                )
            );
            throw;
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private static async ValueTask<HttpResponseMessage> ExecuteAttemptAsync(
        ResilienceContext context,
        (DefaultHttpClientRetryHandler Handler, HttpRequestMessage Request) state
    )
    {
        var response = await state
            .Handler.SendAttemptAsync(state.Request, context.CancellationToken)
            .ConfigureAwait(false);

        if (!context.Properties.GetValue(_bufferingEnabledKey, false))
            return response;

        try
        {
            await response.Content.LoadIntoBufferAsync(context.CancellationToken).ConfigureAwait(false);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static ResiliencePipeline<HttpResponseMessage> CreateRetryPipeline(int retryCount, TimeSpan? bufferTimeout)
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        if (bufferTimeout is { } timeout)
        {
            var retryDelay = TimeSpan.FromMilliseconds(200);
            var totalTimeout = timeout * (retryCount + 1);
            for (var retryIndex = 1; retryIndex <= retryCount; retryIndex++)
                totalTimeout += TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * Math.Pow(2, retryIndex - 1));

            builder.AddTimeout(new TimeoutStrategyOptions { Timeout = totalTimeout });
        }

        if (retryCount > 0)
        {
            builder.AddRetry(
                new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = static args =>
                    {
                        var sdkBuffering = args.Context.Properties.GetValue(_bufferingEnabledKey, false);
                        var requestMethod = args.Context.Properties.GetValue(_requestMethodKey, string.Empty);
                        if (sdkBuffering && requestMethod is not ("GET" or "HEAD"))
                            return ValueTask.FromResult(false);

                        if (args.Outcome.Result is { } response)
                        {
                            var statusCode = (int)response.StatusCode;
                            var shouldRetry = sdkBuffering
                                ? statusCode is 408 or 429 or >= 500 and <= 599
                                : response.StatusCode
                                    is HttpStatusCode.RequestTimeout
                                        or HttpStatusCode.InternalServerError
                                        or HttpStatusCode.BadGateway
                                        or HttpStatusCode.ServiceUnavailable
                                        or HttpStatusCode.GatewayTimeout;
                            return ValueTask.FromResult(shouldRetry);
                        }

                        if (sdkBuffering)
                        {
                            if (args.Context.CancellationToken.IsCancellationRequested)
                                return ValueTask.FromResult(false);

                            return ValueTask.FromResult(
                                args.Outcome.Exception
                                    is HttpRequestException
                                        or IOException
                                        or TimeoutRejectedException
                                        or TaskCanceledException
                            );
                        }

                        return ValueTask.FromResult(
                            args.Outcome.Exception
                                is HttpRequestException
                                    or TimeoutException
                                    or IOException
                                    or TaskCanceledException
                        );
                    },
                    MaxRetryAttempts = retryCount,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = false,
                    OnRetry = static args =>
                    {
                        args.Context.Properties.Set(_latestRetryAttemptKey, args.AttemptNumber + 1);
                        args.Outcome.Result?.Dispose();

                        var requestUri = args.Context.Properties.GetValue(_requestUriKey, "unknown");
                        var retryCountValue = args.Context.Properties.GetValue(_retryCountKey, 0);
                        var progressCallback = args.Context.Properties.GetValue(_progressCallbackKey, null);
                        progressCallback?.Invoke(
                            CreateRetryProgress(
                                args.Outcome,
                                requestUri,
                                args.AttemptNumber + 1,
                                retryCountValue,
                                args.RetryDelay
                            )
                        );

                        var logger = args.Context.Properties.GetValue(_loggerKey, null);
                        if (logger is not null)
                            logger
                                .Here()
                                .Warning(
                                    "HTTP request to {RequestUri} failed, retrying attempt {RetryAttempt} of {RetryCount} in {RetryDelay}",
                                    requestUri,
                                    args.AttemptNumber + 1,
                                    retryCountValue,
                                    args.RetryDelay
                                );

                        return default;
                    },
                }
            );
        }
        if (bufferTimeout is { } timeoutValue)
            builder.AddTimeout(new TimeoutStrategyOptions { Timeout = timeoutValue });

        return builder.Build();
    }

    private async ValueTask<HttpResponseMessage> SendAttemptAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var requestClone = await CloneAsync(request, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(requestClone, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestRetryProgress CreateRetryProgress(
        Outcome<HttpResponseMessage> outcome,
        string requestUri,
        int retryAttempt,
        int retryCount,
        TimeSpan retryDelay
    )
    {
        if (outcome.Exception is not null)
            return CreateProgressFromException(
                outcome.Exception,
                requestUri,
                retryAttempt,
                retryCount,
                retryDelay,
                false
            );

        return CreateProgressFromResponse(outcome.Result!, requestUri, retryAttempt, retryCount, retryDelay, false);
    }

    private static HttpRequestRetryProgress CreateCompletedProgress(
        HttpResponseMessage response,
        string requestUri,
        int retryAttempt,
        int retryCount
    ) => CreateProgressFromResponse(response, requestUri, retryAttempt, retryCount, TimeSpan.Zero, true);

    private static HttpRequestRetryProgress CreateCompletedProgress(
        Exception exception,
        string requestUri,
        int retryAttempt,
        int retryCount
    ) => CreateProgressFromException(exception, requestUri, retryAttempt, retryCount, TimeSpan.Zero, true);

    private static HttpRequestRetryProgress CreateProgressFromResponse(
        HttpResponseMessage response,
        string requestUri,
        int retryAttempt,
        int retryCount,
        TimeSpan retryDelay,
        bool completed
    )
    {
        var statusCode = (int)response.StatusCode;
        var connectionSuccessful = response.IsSuccessStatusCode;

        return new HttpRequestRetryProgress
        {
            RetryAttemptIndex = retryAttempt,
            RetryAttemptCount = retryCount,
            TimeToNextRetry = (int)Math.Ceiling(retryDelay.TotalSeconds),
            StatusCode = statusCode,
            ConnectionSuccessful = connectionSuccessful,
            Completed = completed,
            Message = CreateMessage(
                requestUri,
                statusCode,
                retryAttempt,
                retryCount,
                retryDelay,
                completed,
                connectionSuccessful
            ),
            ErrorMessage = response.ReasonPhrase ?? string.Empty,
            RequestUri = requestUri,
        };
    }

    private static HttpRequestRetryProgress CreateProgressFromException(
        Exception exception,
        string requestUri,
        int retryAttempt,
        int retryCount,
        TimeSpan retryDelay,
        bool completed
    )
    {
        var statusCode = exception switch
        {
            TaskCanceledException => (int)HttpStatusCode.RequestTimeout,
            HttpRequestException => (int)HttpStatusCode.BadGateway,
            TimeoutException => (int)HttpStatusCode.GatewayTimeout,
            TimeoutRejectedException => (int)HttpStatusCode.GatewayTimeout,
            _ => (int)HttpStatusCode.InternalServerError,
        };

        return new HttpRequestRetryProgress
        {
            RetryAttemptIndex = retryAttempt,
            RetryAttemptCount = retryCount,
            TimeToNextRetry = (int)Math.Ceiling(retryDelay.TotalSeconds),
            StatusCode = statusCode,
            ConnectionSuccessful = false,
            Completed = completed,
            Message = CreateMessage(requestUri, statusCode, retryAttempt, retryCount, retryDelay, completed, false),
            ErrorMessage = exception.Message,
            RequestUri = requestUri,
        };
    }

    private static string CreateMessage(
        string requestUri,
        int statusCode,
        int retryAttempt,
        int retryCount,
        TimeSpan retryDelay,
        bool completed,
        bool connectionSuccessful
    )
    {
        if (connectionSuccessful)
            return "Request successful!";

        if (!completed)
        {
            var delay = (int)Math.Ceiling(retryDelay.TotalSeconds);
            return statusCode == (int)HttpStatusCode.RequestTimeout
                ? $"Request to: {requestUri} timed-out, waiting {delay} seconds before retrying again ({retryAttempt} of {retryCount})"
                : $"Request to: {requestUri} failed, waiting {delay} seconds before retrying again ({retryAttempt} of {retryCount})";
        }

        return statusCode == (int)HttpStatusCode.RequestTimeout
            ? $"Request to: {requestUri} timed-out."
            : $"Request to: {requestUri} failed.";
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content is not null)
        {
            var memoryStream = new MemoryStream();
            await request.Content.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            memoryStream.Position = 0;
            clone.Content = new StreamContent(memoryStream);

            foreach (var header in request.Content.Headers)
                clone.Content.Headers.Add(header.Key, header.Value);
        }

        clone.Version = request.Version;
        clone.VersionPolicy = request.VersionPolicy;

        foreach (var option in request.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
