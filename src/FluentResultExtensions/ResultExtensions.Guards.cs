using Microsoft.AspNetCore.Http;

namespace Reaparr.FluentResultExtensions;

public static partial class ResultExtensions
{
    #region General

    /// <summary>
    /// Add the list of errors to the first error found as a nested error list.
    /// </summary>
    /// <param name="result"></param>
    /// <param name="errors"></param>
    /// <returns></returns>
    public static Result AddNestedErrors(this Result result, List<IError> errors)
    {
        var resultErrors = result.Errors;
        if (resultErrors.Count > 0)
            resultErrors[0].Reasons.AddRange(errors);

        return result;
    }

    #endregion

    #region Guards

    public static Result IsNull(string parameterName) =>
        Create400BadRequestResult($"The {parameterName} parameter is null.");

    public static Result ServerIsDisabled(string plexServerName, int entityId, string operation) =>
        CreateErrorStatusCodeResult(
            StatusCodes.Status409Conflict,
            $"The server {plexServerName} with id {entityId} is disabled and can therefore not execute operation: {operation}"
        );

    public static Result IsZero(string parameterName) =>
        Create400BadRequestResult($"The {parameterName} parameter was 0");

    public static Result IsEmpty(string parameterName) =>
        Create400BadRequestResult($"The {parameterName} parameter was empty");

    public static Result IsEmptyQueryResult(string queryName, string entityName, int entityId) =>
        Create204NoContentResult($"The {queryName} returned an empty result with {entityName} id: {entityId}");

    public static Result<T> IsEmptyQueryResult<T>(T value, string queryName, string entityName, int entityId) =>
        Create204NoContentResult(value, $"The {queryName} returned an empty result with {entityName} id: {entityId}");

    public static Result IsInvalidId(string parameterName, int value = 0) =>
        Create400BadRequestResult($"The {parameterName} parameter has an invalid id of {value}");

    public static Result IsInvalidId(string parameterName, string value = "") =>
        Create400BadRequestResult($"The {parameterName} parameter has an invalid id of {value}");

    public static Result IsInvalidId(string parameterName, Guid value) =>
        Create400BadRequestResult($"The {parameterName} parameter has an invalid id of {value}");

    /// <summary>
    /// Creates a new Result with a 404NotFound error set.
    /// </summary>
    /// <param name="entityType"> The name of the entity, e.g. nameof(EntityType).</param>
    /// <param name="id">The entityId which failed to be found.</param>
    /// <returns>A <see cref="Result">Result.Fail()</see> with a 404 Error.</returns>
    public static Result EntityNotFound(string entityType, int id) =>
        Create404NotFoundResult($"The entity of type {entityType} with id {id} could not be found");

    public static Result EntityNotFound(string entityType, string data) =>
        Create404NotFoundResult($"The entity of type {entityType} with data {data} could not be found");

    public static Result EntityNotFound(string entityType, Guid guid) =>
        Create404NotFoundResult($"The entity of type {entityType} with id {guid} could not be found");

    public static Result TaskIsCancelled(string taskName) =>
        Result.Fail(new ExceptionalError(new OperationCanceledException($"The task {taskName} was cancelled")));

    #endregion

    #region Check

    extension(Result result)
    {
        public bool HasException => result.HasError<ExceptionalError>();
    }

    extension(ResultBase result)
    {
        public bool IsCancelled =>
            result.Reasons.OfType<ExceptionalError>().Any(static error => error.Exception is OperationCanceledException);

        public bool IsCancelledOrFailed => result.IsFailed || result.IsCancelled;
    }

    #endregion
}
