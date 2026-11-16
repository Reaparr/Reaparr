namespace Reaparr.FluentResultExtension.UnitTests;

public class ResultExtensionsGuardsTests
{
    #region General

    [Test]
    public void ShouldHaveAddedErrors_WhenAddNestedErrorsIsCalled()
    {
        // Arrange
        var result = Result.Fail("Main Error");

        var errors = new List<IError>
        {
            new Error("Error #1"),
            new Error("Error #2"),
            new Error("Error #3"),
            new Error("Error #4"),
            new Error("Error #5"),
        };

        // Act
        var resultWithErrors = result.AddNestedErrors(errors);

        // Assert
        resultWithErrors.Errors.Count.ShouldBe(1);
        resultWithErrors.Errors.First().Reasons.Count.ShouldBe(5);
    }

    [Test]
    public void ShouldFormatEmptyValue_WhenErrorArgumentIsNull()
    {
        // Arrange
        var result = Result.Fail("Value: {Value}", [null!]);

        // Act
        var errorMessage = result.Errors.Single().Message;

        // Assert
        errorMessage.ShouldBe("Value: ");
    }

    #endregion

    [Test]
    public void ShouldHave400BadRequestError_WhenIsNullIsCalled()
    {
        // Arrange
        var result = ResultExtensions.IsNull("ParameterXYZ");

        // Act
        var has400BadRequestError = result.Has400BadRequestError();

        // Assert
        has400BadRequestError.ShouldBeTrue();
    }

    [Test]
    public void ShouldHave400BadRequestError_WhenIsEmptyIsCalled()
    {
        // Arrange
        var result = ResultExtensions.IsEmpty("ParameterXYZ");

        // Act
        var has400BadRequestError = result.Has400BadRequestError();

        // Assert
        has400BadRequestError.ShouldBeTrue();
    }

    [Test]
    public void ShouldHave400BadRequestError_WhenIsInvalidIdIsCalled()
    {
        // Arrange
        var result = ResultExtensions.IsInvalidId("parameter", "ParameterXYZ");

        // Act
        var has400BadRequestError = result.Has400BadRequestError();

        // Assert
        has400BadRequestError.ShouldBeTrue();
    }

    [Test]
    public void ShouldHave404NotFoundError_WhenEntityNotFoundIsCalled()
    {
        // Arrange
        var result = ResultExtensions.EntityNotFound("ParameterXYZ", 0);

        // Act
        var has404NotFoundError = result.Has404NotFoundError();

        // Assert
        has404NotFoundError.ShouldBeTrue();
    }

    [Test]
    public void ShouldRecognizeCancellation_WhenTypedAndUntypedResultsAreViewedThroughBaseContract()
    {
        // Arrange
        var cancellation = new ExceptionalError(new OperationCanceledException());
        var untyped = Result.Fail(cancellation);
        var typed = Result.Fail<int>(cancellation);
        ResultBase[] results = [untyped, typed];

        // Act
        var baseCancellation = results.Select(result => result.IsCancelled).ToArray();

        // Assert
        baseCancellation.ShouldBe(new[] { true, true });
        untyped.IsCancelled.ShouldBeTrue();
        typed.IsCancelled.ShouldBeTrue();
        untyped.IsCancelledOrFailed.ShouldBeTrue();
        typed.IsCancelledOrFailed.ShouldBeTrue();
        foreach (var result in results)
        {
            result.IsCancelled.ShouldBeTrue();
            result.IsCancelledOrFailed.ShouldBeTrue();
            result.Errors.ShouldHaveSingleItem().ShouldBeSameAs(cancellation);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ShouldDistinguishOrdinaryFailureFromCancellation_WhenUsingBaseContract(bool failed)
    {
        // Arrange
        ResultBase[] results =
        [
            failed ? Result.Fail("Rejected") : Result.Ok(),
            failed ? Result.Fail<int>(new ExceptionalError(new IOException("Unavailable"))) : Result.Ok(42),
        ];

        // Act
        var baseFailures = results.Select(result => result.IsCancelledOrFailed).ToArray();

        // Assert
        baseFailures.ShouldBe(new[] { failed, failed });
        foreach (var result in results)
        {
            result.IsCancelled.ShouldBeFalse();
            result.IsCancelledOrFailed.ShouldBe(failed);
            result.Errors.Count.ShouldBe(failed ? 1 : 0);
        }
    }

    [Test]
    public void ShouldPreserveTopLevelCancellationSemantics_WhenCancellationIsOnlyANestedCause()
    {
        // Arrange
        ResultBase result = Result.Fail(
            new Error("Dependency failure").CausedBy(new ExceptionalError(new OperationCanceledException()))
        );

        // Act
        var cancelled = result.IsCancelled;

        // Assert
        cancelled.ShouldBeFalse();
        result.IsCancelledOrFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Reasons.ShouldHaveSingleItem().ShouldBeOfType<ExceptionalError>();
    }
}
