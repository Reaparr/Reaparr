using FluentValidation.Results;

namespace Reaparr.FluentResultExtensions;

public static class FluentValidationExtensions
{
    public static Result ToResult(this ValidationResult validationResult)
    {
        if (!validationResult.IsValid)
        {
            var result = ResultExtensions.Create400BadRequestResult("Fluent Validation Pipeline Failed.");
            foreach (var reason in validationResult.Errors)
                result.WithError(new Error(reason.ErrorMessage));

            return result;
        }

        return Result.Ok();
    }

    public static TResponse ToResult<TResponse>(this ValidationResult validationResult)
        where TResponse : ResultBase, new()
    {
        var result = new TResponse();

        if (!validationResult.IsValid)
        {
            var error = ResultExtensions.Create400BadRequestResult("Fluent Validation Pipeline Failed.").Errors.First();
            error.Reasons.AddRange(validationResult.Errors.Select(reason => new Error(reason.ErrorMessage)));

            result.Reasons.Add(error);
        }

        return result;
    }
}
