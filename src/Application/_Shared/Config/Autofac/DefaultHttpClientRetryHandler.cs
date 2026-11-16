using System.Net;
using Polly;
using Polly.Retry;

namespace Reaparr.Application;

internal sealed class DefaultHttpClientRetryHandler(ILogger log) : DelegatingHandler
{
    private const int MAX_RETRY_ATTEMPTS = 3;

    private readonly ILogger _log = log.ForContext<DefaultHttpClientRetryHandler>();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var requestUri = request.RequestUri?.ToString() ?? "unknown";
        var retryCount = Math.Max(request.GetRetryCount() ?? MAX_RETRY_ATTEMPTS, 0);
        var progressCallback = request.GetRetryProgressCallback();
        var latestRetryAttempt = 0;

        if (retryCount == 0)
        {
            try
            {
                var requestClone = await CloneAsync(request, cancellationToken);
                var response = await base.SendAsync(requestClone, cancellationToken);
                progressCallback?.Invoke(CreateCompletedProgress(response, requestUri, latestRetryAttempt, retryCount));
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke(CreateCompletedProgress(ex, requestUri, latestRetryAttempt, retryCount));
                throw;
            }
        }

        var retryPipeline = CreateRetryPipeline(requestUri, retryCount, progress =>
        {
            latestRetryAttempt = progress.RetryAttemptIndex;
            progressCallback?.Invoke(progress);
        });

        try
        {
            var response = await retryPipeline.ExecuteAsync(
                async token =>
                {
                    var requestClone = await CloneAsync(request, token);
                    return await base.SendAsync(requestClone, token);
                },
                cancellationToken
            );

            progressCallback?.Invoke(CreateCompletedProgress(response, requestUri, latestRetryAttempt, retryCount));
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke(CreateCompletedProgress(ex, requestUri, latestRetryAttempt, retryCount));
            throw;
        }
    }

    private ResiliencePipeline<HttpResponseMessage> CreateRetryPipeline(
        string requestUri,
        int retryCount,
        Action<HttpRequestRetryProgress> onRetryProgress
    )
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(
                new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutException>()
                        .Handle<IOException>()
                        .Handle<TaskCanceledException>()
                        .HandleResult(response =>
                            response.StatusCode
                                is HttpStatusCode.RequestTimeout
                                    or HttpStatusCode.InternalServerError
                                    or HttpStatusCode.BadGateway
                                    or HttpStatusCode.ServiceUnavailable
                                    or HttpStatusCode.GatewayTimeout
                        ),
                    MaxRetryAttempts = retryCount,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = false,
                    OnRetry = args =>
                    {
                        args.Outcome.Result?.Dispose();

                        var progress = CreateRetryProgress(
                            args.Outcome,
                            requestUri,
                            args.AttemptNumber + 1,
                            retryCount,
                            args.RetryDelay
                        );
                        onRetryProgress(progress);

                        _log.Here()
                            .Warning(
                                "HTTP request to {RequestUri} failed, retrying attempt {RetryAttempt} of {RetryCount} in {RetryDelay}",
                                requestUri,
                                args.AttemptNumber + 1,
                                retryCount,
                                args.RetryDelay
                            );

                        return default;
                    },
                }
            )
            .Build();
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
