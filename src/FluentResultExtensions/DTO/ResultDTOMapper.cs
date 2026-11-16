using Microsoft.AspNetCore.Http;

namespace Reaparr.FluentResultExtensions;

// ReSharper disable InconsistentNaming
public static class ResultDTOMapper
{
    public static BaseResultDTO ToResultDTO(this Result result) =>
        new()
        {
            IsSuccess = result.IsSuccess,
            Errors = result.Errors.ToErrorDTOs(),
            Successes = result.Successes.ToSuccessDTOs(),
            StatusCode = result.ToStatusCode(),
        };

    public static ResultDTO<T> ToResultDTO<T>(this Result<T> result) =>
        new()
        {
            Value = result.ValueOrDefault,
            IsSuccess = result.IsSuccess,
            Errors = result.Errors.ToErrorDTOs(),
            Successes = result.Successes.ToSuccessDTOs(),
            StatusCode = result.ToResult().ToStatusCode(),
        };

    public static ResultDTO<TDTO> ToResultDTO<T, TDTO>(this Result<T> result, Func<T, TDTO> mapper) =>
        new()
        {
            Value = result.ValueOrDefault != null ? mapper(result.ValueOrDefault) : default,
            IsSuccess = result.IsSuccess,
            Errors = result.Errors.ToErrorDTOs(),
            Successes = result.Successes.ToSuccessDTOs(),
            StatusCode = result.ToResult().ToStatusCode(),
        };

    private static IReadOnlyList<ErrorDTO> ToErrorDTOs(this IReadOnlyList<IError> reasons) =>
        reasons
            .Select(x => new ErrorDTO
            {
                Message = x.Message,
                Reasons = x.Reasons.ToErrorDTOs().ToList(),
                Metadata = x.Metadata,
            })
            .ToList();

    private static IReadOnlyList<SuccessDTO> ToSuccessDTOs(this IReadOnlyList<ISuccess> reasons) =>
        reasons.Select(x => new SuccessDTO { Message = x.Message, Metadata = x.Metadata }).ToList();

    private static int ToStatusCode(this Result result)
    {
        if (result.IsSuccess)
        {
            // Status code 201 Created
            if (result.Has201CreatedRequestSuccess())
                return StatusCodes.Status201Created;

            // Status code 204 No Content
            if (result.Has204NoContentRequestSuccess())
                return StatusCodes.Status204NoContent;

            // Status code 200 Ok
            return StatusCodes.Status200OK;
        }

        // Status Code 400 Bad Request
        if (result.Has400BadRequestError())
            return StatusCodes.Status400BadRequest;

        // Status Code 401 Unauthorized
        if (result.Has401UnauthorizedError())
            return StatusCodes.Status401Unauthorized;

        // Keep Plex authentication failures distinct from application-session authentication.
        if (result.HasPlex401UnauthorizedError())
            return HttpCodes.Status601PlexAuthenticationFailed;

        // Status Code 403 Forbidden
        if (result.Has403ForbiddenError())
            return StatusCodes.Status403Forbidden;

        // Status Code 404 Not Found
        if (result.Has404NotFoundError())
            return StatusCodes.Status404NotFound;

        // Status Code 409 Conflict
        if (result.HasStatusCode(StatusCodes.Status409Conflict))
            return StatusCodes.Status409Conflict;

        // Status Code 502 Bad Gateway
        if (result.Has502BadGatewayError())
            return StatusCodes.Status502BadGateway;

        // Status Code 504 Gateway Timeout
        if (result.Has504GatewayTimeoutError())
            return StatusCodes.Status504GatewayTimeout;

        // Status Code 503 Service Unavailable
        if (result.Has503ServiceUnavailableError())
            return StatusCodes.Status503ServiceUnavailable;

        // Status Code 500 Internal Server Error
        return StatusCodes.Status500InternalServerError;
    }
}
