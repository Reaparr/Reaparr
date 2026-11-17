using System.Net;
using System.Text.Json;
using LukeHagar.PlexAPI.SDK.Models.Errors;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Reaparr.PlexApi;

public static class HttpClientExtensions
{
    /// <summary>
    /// Converts a generated Plex SDK operation into a Result while preserving its raw response and exception.
    /// </summary>
    /// <param name="operation"> The SpeakEasy Plex SDK endpoint method to convert the result for </param>
    /// <param name="cancellationToken">A token used to classify caller-requested cancellation; HTTP request cancellation is bound when the client is created.</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static async Task<Result<T>> ToResponse<T>(
        this Task<T> operation,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        try
        {
            var response = await operation.ConfigureAwait(false);
            var httpResponse = response.GetHttpResponseMessage();
            return httpResponse.IsSuccessStatusCode
                ? Result.Ok(response).AddStatusCode(httpResponse.StatusCode)
                : httpResponse.FromSdkExceptionToResult<T>();
        }
        catch (SDKException sdkException)
        {
            return sdkException.RawResponse.FromSdkExceptionToResult<T>().WithError(new ExceptionalError(sdkException));
        }
        catch (ResponseValidationException responseValidationException)
        {
            return Result
                .Fail<T>(
                    new ExceptionalError(responseValidationException).WithMetadata(
                        "json body",
                        responseValidationException.Body
                    )
                )
                .LogError();
        }
        catch (PlexAPIError plexApiError)
        {
            var errors = GetPlexErrors(plexApiError);
            return plexApiError
                .RawResponse.FromSdkExceptionToResult<T>(errors)
                .WithError(new ExceptionalError(plexApiError));
        }
        catch (JsonSerializationException jsonSerializationException)
        {
            return Result.Fail<T>(new ExceptionalError(jsonSerializationException)).LogError();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Fail<T>((FluentResults.Error)ResultExtensions.TaskIsCancelled(nameof(ToResponse)).Errors[0]);
        }
        catch (TaskCanceledException taskCanceledException)
        {
            return Result.Fail<T>(new ExceptionalError(taskCanceledException)).Add408RequestTimeoutError().LogWarning();
        }
        catch (Polly.Timeout.TimeoutRejectedException timeoutRejectedException)
        {
            return Result
                .Fail<T>(new ExceptionalError(timeoutRejectedException))
                .Add408RequestTimeoutError()
                .LogWarning();
        }
        catch (HttpRequestException httpRequestException)
        {
            return Result.Fail<T>(new ExceptionalError(httpRequestException)).Add502BadGatewayError().LogWarning();
        }
        catch (Exception exception)
        {
            var rawResponse =
                exception.GetType().GetProperty(nameof(PostUsersSignInDataResponse.RawResponse))?.GetValue(exception)
                as HttpResponseMessage;
            var errorsValue = exception.GetType().GetProperty("Errors")?.GetValue(exception);
            if (rawResponse is not null)
            {
                var errors = errorsValue is null
                    ? null
                    : JsonSerializer.Deserialize<List<PlexError>>(JsonSerializer.Serialize(errorsValue));
                return rawResponse.FromSdkExceptionToResult<T>(errors).WithError(new ExceptionalError(exception));
            }

            return Result.Fail<T>(new ExceptionalError(exception)).LogError();
        }
    }

    private static List<PlexError>? GetPlexErrors(PlexAPIError exception)
    {
        var payload = exception.GetType().GetProperty("Payload")?.GetValue(exception);
        var errors = payload?.GetType().GetProperty("Errors")?.GetValue(payload)
            ?? exception.GetType().GetProperty("Errors")?.GetValue(exception);
        return errors is null ? null : JsonSerializer.Deserialize<List<PlexError>>(JsonSerializer.Serialize(errors));
    }

    public static Result<TResult> ToApiResult<TResponse, TResult>(
        this Result<TResponse> response,
        Func<TResponse, TResult> mapper
    )
        where TResponse : class
    {
        if (response.IsSuccess)
        {
            var httpResponseMessage = response.Value.GetHttpResponseMessage();
            return Result.Ok(mapper(response.Value)).AddStatusCode(httpResponseMessage.StatusCode);
        }

        // TODO Might need to add more error handling here such as AddStatusCode
        return response.ToResult();
    }

    public static Result<TResult> FromSdkExceptionToResult<TResult>(
        this HttpResponseMessage response,
        List<PlexError>? errors = null
    )
        where TResult : class
    {
        switch (response.StatusCode)
        {
            // In case of unauthorized
            case HttpStatusCode.Unauthorized:
                return Result.Fail<TResult>("Unauthorized").AddPlex401UnauthorizedError().WithErrors(errors ?? []);

            // In case of timeout
            case HttpStatusCode.RequestTimeout:
                return Result.Fail<TResult>("Request timed out").Add408RequestTimeoutError().WithErrors(errors ?? []);
        }

        // Weird case where the status code is 200 but the content is "Bad Gateway"
        if (response.IsSuccessStatusCode && response.Content.ToString()!.Contains("Bad Gateway"))
            return Result
                .Fail<TResult>("Server responded with Bad Gateway")
                .Add502BadGatewayError()
                .WithErrors(errors ?? []);

        return Result.Fail<TResult>("Request failed").AddStatusCode(response.StatusCode).WithErrors(errors ?? []);
    }

    internal static HttpResponseMessage GetHttpResponseMessage<T>(this T response) =>
        (
            typeof(T).GetProperty(nameof(PostUsersSignInDataResponse.RawResponse))!.GetValue(response)
            as HttpResponseMessage
        )!;

    public static async Task<string> ReadAsFormattedJsonAsync(
        this HttpContent? content,
        CancellationToken cancellationToken = default
    )
    {
        if (content is null)
            return "HttpContent is null.";

        var contentType = content.Headers.ContentType?.MediaType;
        var contentLength = content.Headers.ContentLength;

        // Check if the content indicates a file download via Content-Disposition header.
        if (
            content.Headers.ContentDisposition is not null
            && content.Headers.ContentDisposition.DispositionType.Equals(
                "attachment",
                StringComparison.OrdinalIgnoreCase
            )
        )
            return $"Content is a file download. Filename: {content.Headers.ContentDisposition.FileName}";

        // Only process JSON content types
        if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
        {
            var sizeInfo = contentLength.HasValue ? $"{contentLength.Value:N0} bytes" : "unknown size";
            return $"Non-JSON content ({contentType ?? "unknown"}, {sizeInfo}) - not formatting as JSON.";
        }

        // Check content length to avoid buffer overflow for large JSON responses
        const long maxReadContentSize = 1024 * 1024;
        if (contentLength is > maxReadContentSize)
            return $"Large JSON content ({contentLength.Value:N0} bytes) - not reading to avoid buffer overflow.";

        try
        {
            var stringResponse = await content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(stringResponse))
                return "Content is empty.";

            using var doc = JsonDocument.Parse(stringResponse);
            return JsonSerializer.Serialize(doc, DefaultJsonSerializerOptions.UserSettingsOptions);
        }
        catch (JsonException)
        {
            return "Content is not valid JSON.";
        }
    }
}
