using System.Net;

namespace Reaparr.FluentResultExtensions;

public static partial class ResultExtensions
{
    private const string NETWORK_TIMEOUT_TOKEN = "network timeout";
    private const string TIMED_OUT_TOKEN = "timed out";

    #region Properties

    public static string StatusCodeName => "StatusCode";

    public static string ErrorMessageName => "ErrorMessage";

    #endregion

    #region Implementation

    #region HasStatusCode

    public static bool HasStatusCode(this ResultBase result, int statusCode = 0)
    {
        foreach (var reason in result.Reasons)
        foreach (var (key, metaData) in reason.Metadata)
            if (key == StatusCodeName && (statusCode == 0 || (int)metaData == statusCode))
                return true;

        return false;
    }
    #endregion

    #region GetStatusCodeError

    public static IReason? GetStatusCodeReason(this Result result)
    {
        return result.Reasons.Find(x => x.HasMetadataKey(StatusCodeName));
    }

    public static IReason? GetStatusCodeReason<T>(this Result<T> result)
    {
        return result.Reasons.Find(x => x.HasMetadataKey(StatusCodeName));
    }

    #endregion

    #region AddStatusCode

    public static Result AddStatusCode(this Result result, HttpStatusCode statusCode, string msg = "")
    {
        return statusCode switch
        {
            HttpStatusCode.OK => result.Add200OkRequestSuccess(msg),
            HttpStatusCode.Created => result.Add201CreatedRequestSuccess(msg),
            HttpStatusCode.NoContent => result.Add204NoContentRequestSuccess(msg),
            HttpStatusCode.Accepted => result.AddStatusCodeSuccess((int)HttpStatusCode.Accepted, msg),
            HttpStatusCode.BadRequest => result.Add400BadRequestError(msg),
            HttpStatusCode.Unauthorized => result.Add401UnauthorizedError(msg),
            HttpStatusCode.Forbidden => result.Add403ForbiddenError(msg),
            HttpStatusCode.NotFound => result.Add404NotFoundError(msg),
            HttpStatusCode.RequestTimeout => result.Add408RequestTimeoutError(msg),
            HttpStatusCode.TooManyRequests => result.Add429TooManyRequestsError(msg),
            HttpStatusCode.InternalServerError => result.Add500InternalServerError(msg),
            HttpStatusCode.BadGateway => result.Add502BadGatewayError(msg),
            HttpStatusCode.ServiceUnavailable => result.Add503ServiceUnavailableError(msg),
            HttpStatusCode.GatewayTimeout => result.Add504GatewayTimeoutError(msg),
            _ when (int)statusCode >= 200 && (int)statusCode < 300 => result.AddStatusCodeSuccess((int)statusCode, msg),
            _ => result.AddStatusCodeError((int)statusCode, msg),
        };
    }

    public static Result<T> AddStatusCode<T>(this Result<T> result, HttpStatusCode statusCode, string msg = "")
    {
        return statusCode switch
        {
            HttpStatusCode.OK => result.Add200OkRequestSuccess(msg),
            HttpStatusCode.Created => result.Add201CreatedRequestSuccess(msg),
            HttpStatusCode.NoContent => result.Add204NoContentRequestSuccess(msg),
            HttpStatusCode.Accepted => result.AddStatusCodeSuccess((int)HttpStatusCode.Accepted, msg),
            HttpStatusCode.BadRequest => result.Add400BadRequestError(msg),
            HttpStatusCode.Unauthorized => result.Add401UnauthorizedError(msg),
            HttpStatusCode.Forbidden => result.Add403ForbiddenError(msg),
            HttpStatusCode.NotFound => result.Add404NotFoundError(msg),
            HttpStatusCode.RequestTimeout => result.Add408RequestTimeoutError(msg),
            HttpStatusCode.TooManyRequests => result.Add429TooManyRequestsError(msg),
            HttpStatusCode.InternalServerError => result.Add500InternalServerError(msg),
            HttpStatusCode.BadGateway => result.Add502BadGatewayError(msg),
            HttpStatusCode.ServiceUnavailable => result.Add503ServiceUnavailableError(msg),
            HttpStatusCode.GatewayTimeout => result.Add504GatewayTimeoutError(msg),
            _ when (int)statusCode >= 200 && (int)statusCode < 300 => result.AddStatusCodeSuccess((int)statusCode, msg),
            _ => result.AddStatusCodeError((int)statusCode, msg),
        };
    }

    #endregion

    #region FindStatusCode

    public static int FindStatusCode(this Result result)
    {
        foreach (var reason in result.Reasons)
        foreach (var (key, metaData) in reason.Metadata)
            if (key == StatusCodeName)
                return (int)metaData;

        return 0;
    }

    public static bool IsServerUnreachable(this Result result) =>
        result.Has408RequestTimeout()
        || result.Has500InternalServerError()
        || result.Has502BadGatewayError()
        || result.Has503ServiceUnavailableError()
        || result.Has504GatewayTimeoutError()
        || result.Errors.Any(error =>
            error.Message.Contains(NETWORK_TIMEOUT_TOKEN, StringComparison.OrdinalIgnoreCase)
            || error.Message.Contains(TIMED_OUT_TOKEN, StringComparison.OrdinalIgnoreCase)
            || (
                error.Metadata.TryGetValue(ErrorMessageName, out var meta)
                && meta is string metaStr
                && (
                    metaStr.Contains(NETWORK_TIMEOUT_TOKEN, StringComparison.OrdinalIgnoreCase)
                    || metaStr.Contains(TIMED_OUT_TOKEN, StringComparison.OrdinalIgnoreCase)
                )
            )
        );

    public static bool IsServerUnreachable<T>(this Result<T> result) => result.ToResult().IsServerUnreachable();

    #endregion

    private static Result CreateErrorStatusCodeResult(int statusCode, string message = "") =>
        Result.Fail(GetStatusCodeReason(statusCode, message));

    private static Result CreateSuccessStatusCodeResult(int statusCode, string message = "") =>
        Result.Ok().WithSuccess(GetStatusCodeSuccess(statusCode, message));

    private static Result<T> CreateSuccessStatusCodeResult<T>(T value, int statusCode, string message = "") =>
        Result.Ok(value).WithSuccess(GetStatusCodeSuccess(statusCode, message));

    #region CreateStatusCodeReasons

    private static Error GetStatusCodeReason(int statusCode, string message = "") =>
        new Error(message).WithMetadata(StatusCodeName, statusCode);

    private static Success GetStatusCodeSuccess(int statusCode, string message = "") =>
        new Success(message).WithMetadata(StatusCodeName, statusCode);

    #endregion

    #region AddStatusCodeError

    private static Result AddStatusCodeError(this Result result, int statusCode, string message = "")
    {
        if (string.IsNullOrEmpty(message))
            message = "No error message found";

        if (!result.Errors.Any())
            result.WithError(new Error($"Status code: ({statusCode}) - {message}"));

        result.Errors[0].Metadata.Add(StatusCodeName, statusCode);
        result.Errors[0].Metadata.Add(ErrorMessageName, message);

        return result;
    }

    private static Result<T> AddStatusCodeError<T>(this Result<T> result, int statusCode, string message = "") =>
        result.AddStatusCodeError(GetStatusCodeReason(statusCode, message));

    private static Result<T> AddStatusCodeError<T>(this Result<T> result, Error error) => result.WithError(error);

    #endregion

    #region AddStatusCodeSuccess

    private static Result AddStatusCodeSuccess(this Result result, int statusCode, string message = "") =>
        result.AddStatusCodeSuccess(GetStatusCodeSuccess(statusCode, message));

    private static Result AddStatusCodeSuccess(this Result result, Success success) => result.WithSuccess(success);

    private static Result<T> AddStatusCodeSuccess<T>(this Result<T> result, int statusCode, string message = "") =>
        result.AddStatusCodeSuccess(GetStatusCodeSuccess(statusCode, message));

    private static Result<T> AddStatusCodeSuccess<T>(this Result<T> result, Success success) =>
        result.WithSuccess(success);

    #endregion

    #endregion

    #region Result Signatures

    #region 200

    public static bool Has200OkRequestSuccess(this Result result) => result.HasStatusCode(HttpCodes.Status200OK);

    public static Result Add200OkRequestSuccess(this Result result, string message = "Ok successful") =>
        result.AddStatusCodeSuccess(HttpCodes.Status200OK, message);

    public static Result Create200OkResult(string message = "") =>
        CreateSuccessStatusCodeResult(HttpCodes.Status200OK, message);

    #endregion

    #region 201

    public static bool Has201CreatedRequestSuccess(this Result result) =>
        result.HasStatusCode(HttpCodes.Status201Created);

    public static Result Add201CreatedRequestSuccess(this Result result, string message = "Created successful") =>
        result.AddStatusCodeSuccess(HttpCodes.Status201Created, message);

    public static Result Create201CreatedResult(string message = "") =>
        CreateSuccessStatusCodeResult(HttpCodes.Status201Created, message);

    #endregion

    #region 204

    public static bool Has204NoContentRequestSuccess(this Result result) =>
        result.HasStatusCode(HttpCodes.Status204NoContent);

    public static Result Add204NoContentRequestSuccess(this Result result, string message = "No Content") =>
        result.AddStatusCodeSuccess(HttpCodes.Status204NoContent, message);

    public static Result Create204NoContentResult(string message = "") =>
        CreateSuccessStatusCodeResult(HttpCodes.Status204NoContent, message);

    #endregion

    #region 400

    public static bool Has400BadRequestError(this Result result) => result.HasStatusCode(HttpCodes.Status400BadRequest);

    public static Result Add400BadRequestError(this Result result, string message = "Bad request") =>
        result.AddStatusCodeError(HttpCodes.Status400BadRequest, message);

    public static Result Create400BadRequestResult(string message = "") =>
        CreateErrorStatusCodeResult(HttpCodes.Status400BadRequest, message);

    #endregion

    #region 401

    public static bool Has401UnauthorizedError(this Result result) =>
        result.HasStatusCode(HttpCodes.Status401Unauthorized);

    public static Result Add401UnauthorizedError(this Result result, string message = "Unauthorized") =>
        result.AddStatusCodeError(HttpCodes.Status401Unauthorized, message);

    public static Result Create401UnauthorizedResult(string message = "") =>
        CreateErrorStatusCodeResult(HttpCodes.Status401Unauthorized, message);

    #region 401 - Plex

    /// <summary>
    /// We can't use the standard 401 Unauthorized status code because it will log the frontend user out.
    /// So we map it to 601 which is a custom status code for Plex 401 Unauthorized.
    /// </summary>
    public static bool HasPlex401UnauthorizedError(this Result result) =>
        result.HasStatusCode(HttpCodes.Status601PlexAuthenticationFailed);

    /// <summary>
    /// We can't use the standard 401 Unauthorized status code because it will log the frontend user out.
    /// So we map it to 601 which is a custom status code for Plex 401 Unauthorized.
    /// </summary>
    public static Result AddPlex401UnauthorizedError(this Result result, string message = "Unauthorized") =>
        result.AddStatusCodeError(HttpCodes.Status601PlexAuthenticationFailed, message);

    #endregion

    #endregion

    #region 403

    public static bool Has403ForbiddenError(this Result result) => result.HasStatusCode(HttpCodes.Status403Forbidden);

    public static Result Add403ForbiddenError(this Result result, string message = "Unauthorized") =>
        result.AddStatusCodeError(HttpCodes.Status403Forbidden, message);

    public static Result Create403ForbiddenResult(string message = "") =>
        CreateErrorStatusCodeResult(HttpCodes.Status403Forbidden, message);

    #endregion

    #region 404

    public static bool Has404NotFoundError(this Result result) => result.HasStatusCode(HttpCodes.Status404NotFound);

    public static Result Add404NotFoundError(this Result result, string message = "Not Found") =>
        result.AddStatusCodeError(HttpCodes.Status404NotFound, message);

    public static Result Create404NotFoundResult(string message = "") =>
        CreateErrorStatusCodeResult(HttpCodes.Status404NotFound, message);

    #endregion

    #region 408

    public static bool Has408RequestTimeout(this Result result) =>
        result.HasStatusCode(HttpCodes.Status408RequestTimeout)
        || result.HasException<TimeoutException>()
        || result.HasException<TaskCanceledException>();

    public static Result Add408RequestTimeoutError(this Result result, string message = "Request Timeout") =>
        result.AddStatusCodeError(HttpCodes.Status408RequestTimeout, message);

    public static Result Create408RequestTimeoutError(string message = "") =>
        CreateErrorStatusCodeResult(HttpCodes.Status408RequestTimeout, message);

    #endregion

    #region 429

    public static bool Has429TooManyRequestsError(this Result result) =>
        result.HasStatusCode(HttpCodes.Status429TooManyRequests);

    public static Result Add429TooManyRequestsError(this Result result, string message = "Too Many Requests") =>
        result.AddStatusCodeError(HttpCodes.Status429TooManyRequests, message);

    public static Result Create429TooManyRequestsResult(string message = "") =>
        CreateErrorStatusCodeResult(HttpCodes.Status429TooManyRequests, message);

    #endregion

    #region 500

    public static bool Has500InternalServerError(this Result result) =>
        result.HasStatusCode(HttpCodes.Status500InternalServerError);

    public static Result Add500InternalServerError(this Result result, string message = "Internal Server Error") =>
        result.AddStatusCodeError(HttpCodes.Status500InternalServerError, message);

    public static Result Create500InternalServerErrorResult(string message = "") =>
        CreateErrorStatusCodeResult(HttpCodes.Status500InternalServerError, message);

    #endregion

    #region 502

    public static bool Has502BadGatewayError(this Result result) => result.HasStatusCode(HttpCodes.Status502BadGateway);

    public static Result Add502BadGatewayError(this Result result, string message = "Bad Gateway") =>
        result.AddStatusCodeError(HttpCodes.Status502BadGateway, message);

    public static Result Create502BadGatewayResult(string message = "") =>
        CreateErrorStatusCodeResult(HttpCodes.Status502BadGateway, message);

    #endregion

    #region 503

    public static bool Has503ServiceUnavailableError(this Result result) =>
        result.HasStatusCode(HttpCodes.Status503ServiceUnavailable);

    public static Result Add503ServiceUnavailableError(this Result result, string message = "Service Unavailable") =>
        result.AddStatusCodeError(HttpCodes.Status503ServiceUnavailable, message);

    public static Result Create503ServiceUnavailableResult(string message = "") =>
        CreateErrorStatusCodeResult(HttpCodes.Status503ServiceUnavailable, message);

    #endregion

    #region 504

    public static bool Has504GatewayTimeoutError(this Result result) =>
        result.HasStatusCode(HttpCodes.Status504GatewayTimeout);

    public static Result Add504GatewayTimeoutError(
        this Result result,
        string message = "The server didn't respond in time."
    ) => result.AddStatusCodeError(HttpCodes.Status504GatewayTimeout, message);

    public static Result Create504GatewayTimeoutResult(string message = "The server didn't respond in time.") =>
        CreateErrorStatusCodeResult(HttpCodes.Status504GatewayTimeout, message);

    #endregion

    #endregion

    #region Result<T> Signatures

    #region 200

    public static bool Has200OkRequestSuccess<T>(this Result<T> result) => result.HasStatusCode(HttpCodes.Status200OK);

    public static Result<T> Add200OkRequestSuccess<T>(this Result<T> result, string message = "Ok successful") =>
        result.AddStatusCodeSuccess(HttpCodes.Status200OK, message);

    public static Result<T> Create200OkResult<T>(T value, string message = "") =>
        CreateSuccessStatusCodeResult(value, HttpCodes.Status200OK, message);

    #endregion

    #region 201

    public static bool Has201CreatedRequestSuccess<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status201Created);

    public static Result<T> Add201CreatedRequestSuccess<T>(
        this Result<T> result,
        string message = "Created successful"
    ) => result.AddStatusCodeSuccess(HttpCodes.Status201Created, message);

    public static Result<T> Create201CreatedResult<T>(T value, string message = "") =>
        CreateSuccessStatusCodeResult(value, HttpCodes.Status201Created, message);

    #endregion

    #region 204

    public static bool Has204NoContentRequestSuccess<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status204NoContent);

    public static Result<T> Add204NoContentRequestSuccess<T>(this Result<T> result, string message = "No Content") =>
        result.AddStatusCodeSuccess(HttpCodes.Status204NoContent, message);

    public static Result<T> Create204NoContentResult<T>(T value, string message = "") =>
        CreateSuccessStatusCodeResult(value, HttpCodes.Status204NoContent, message);

    #endregion

    #region 400

    public static bool Has400BadRequestError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status400BadRequest);

    public static Result<T> Add400BadRequestError<T>(this Result<T> result, string message = "Bad request") =>
        result.AddStatusCodeError(HttpCodes.Status400BadRequest, message);

    #endregion

    #region 401

    public static bool Has401UnauthorizedError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status401Unauthorized);

    public static Result<T> Add401UnauthorizedError<T>(this Result<T> result, string message = "Unauthorized") =>
        result.AddStatusCodeError(HttpCodes.Status401Unauthorized, message);

    #endregion

    #region 401 - Plex

    /// <summary>
    /// We can't use the standard 401 Unauthorized status code because it will log the frontend user out.
    /// So we map it to 601 which is a custom status code for Plex 401 Unauthorized.
    /// </summary>
    public static bool HasPlex401UnauthorizedError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status601PlexAuthenticationFailed);

    /// <summary>
    /// We can't use the standard 401 Unauthorized status code because it will log the frontend user out.
    /// So we map it to 601 which is a custom status code for Plex 401 Unauthorized.
    /// </summary>
    public static Result<T> AddPlex401UnauthorizedError<T>(this Result<T> result, string message = "Unauthorized") =>
        result.AddStatusCodeError(HttpCodes.Status601PlexAuthenticationFailed, message);

    #endregion

    #region 403

    public static bool Has403ForbiddenError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status403Forbidden);

    public static Result<T> Add403ForbiddenError<T>(this Result<T> result, string message = "Forbidden") =>
        result.AddStatusCodeError(HttpCodes.Status403Forbidden, message);

    #endregion

    #region 404

    public static bool Has404NotFoundError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status404NotFound);

    public static Result<T> Add404NotFoundError<T>(this Result<T> result, string message = "Not Found") =>
        result.AddStatusCodeError(HttpCodes.Status404NotFound, message);

    #endregion

    #region 408

    public static bool Has408RequestTimeout<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status408RequestTimeout)
        || result.HasException<TimeoutException>()
        || result.HasException<TaskCanceledException>();

    public static Result<T> Add408RequestTimeoutError<T>(this Result<T> result, string message = "Request Timeout") =>
        result.AddStatusCodeError(HttpCodes.Status408RequestTimeout, message);

    #endregion

    #region 429

    public static bool Has429TooManyRequestsError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status429TooManyRequests);

    public static Result<T> Add429TooManyRequestsError<T>(
        this Result<T> result,
        string message = "Too Many Requests"
    ) => result.AddStatusCodeError(HttpCodes.Status429TooManyRequests, message);

    #endregion

    #region 500

    public static bool Has500InternalServerError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status500InternalServerError);

    public static Result<T> Add500InternalServerError<T>(
        this Result<T> result,
        string message = "Internal Server Error"
    ) => result.AddStatusCodeError(HttpCodes.Status500InternalServerError, message);

    #endregion

    #region 502

    public static bool Has502BadGatewayError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status502BadGateway);

    public static Result<T> Add502BadGatewayError<T>(this Result<T> result, string message = "Bad Gateway") =>
        result.AddStatusCodeError(HttpCodes.Status502BadGateway, message);

    #endregion

    #region 503

    public static bool Has503ServiceUnavailableError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status503ServiceUnavailable);

    public static Result<T> Add503ServiceUnavailableError<T>(
        this Result<T> result,
        string message = "Service Unavailable"
    ) => result.AddStatusCodeError(HttpCodes.Status503ServiceUnavailable, message);

    #endregion

    #region 504

    public static bool Has504GatewayTimeoutError<T>(this Result<T> result) =>
        result.HasStatusCode(HttpCodes.Status504GatewayTimeout);

    public static Result<T> Add504GatewayTimeoutError<T>(
        this Result<T> result,
        string message = "The server didn't respond in time."
    ) => result.AddStatusCodeError(HttpCodes.Status504GatewayTimeout, message);

    #endregion

    #endregion

    #region Storage

    private const string NOT_ENOUGH_SPACE_TOKEN = "not enough space";
    private const string DISK_FULL_TOKEN = "disk full";

    public static bool HasStorageError(this Result result) =>
        result.Errors.Any(error =>
            error.Message.Contains(NOT_ENOUGH_SPACE_TOKEN, StringComparison.OrdinalIgnoreCase)
            || error.Message.Contains(DISK_FULL_TOKEN, StringComparison.OrdinalIgnoreCase)
        )
        || result.HasException<IOException>(exception => exception is not HttpIOException)
        || result.HasException<UnauthorizedAccessException>();

    public static bool HasStorageError<T>(this Result<T> result) => result.ToResult().HasStorageError();

    #endregion
}
