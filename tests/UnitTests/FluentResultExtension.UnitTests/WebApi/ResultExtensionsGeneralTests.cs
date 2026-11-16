using System.Net;

namespace Reaparr.FluentResultExtension.UnitTests;

public class ResultExtensionsGeneralTests
{
    [Test]
    public void ShouldNotFindStatusCode_WhenGivenAnInvalidHasStatusCode()
    {
        // Arrange
        var result = new Result().Add404NotFoundError();

        // Act
        var has201CreatedRequest = result.Has201CreatedRequestSuccess();

        // Assert
        has201CreatedRequest.ShouldBeFalse();
    }

    [Test]
    public void ShouldFindStatusCode_WhenResultBaseContainsGenericResult()
    {
        // Arrange
        ResultBase result = Result.Fail<string>("failed").AddStatusCode(HttpStatusCode.BadRequest);

        // Act
        var hasStatusCode = result.HasStatusCode((int)HttpStatusCode.BadRequest);

        // Assert
        hasStatusCode.ShouldBeTrue();
    }

    [Test]
    public void ShouldFindStatusCode_WhenMatchingStatusIsOnLaterReason()
    {
        // Arrange
        var result = Result
            .Ok()
            .WithSuccess(new Success("first reason"))
            .WithSuccess(new Success("later reason").WithMetadata(ResultExtensions.StatusCodeName, 418));

        // Act
        var hasStatusCode = result.HasStatusCode(418);

        // Assert
        hasStatusCode.ShouldBeTrue();
    }

    [Test]
    public void ShouldMatchAnyStatusCode_WhenStatusCodeIsZero()
    {
        // Arrange
        var result = Result.Ok().WithSuccess(new Success("status").WithMetadata(ResultExtensions.StatusCodeName, 418));

        // Act
        var hasStatusCode = result.HasStatusCode();

        // Assert
        hasStatusCode.ShouldBeTrue();
    }

    [Test]
    public void ShouldNotFindStatusCode_WhenNoStatusMetadataExists()
    {
        // Arrange
        var result = Result.Ok().WithSuccess(new Success("without status"));

        // Act
        var hasStatusCode = result.HasStatusCode();

        // Assert
        hasStatusCode.ShouldBeFalse();
    }

    [Test]
    public void ShouldFindStatusCode_WhenMatchingStatusIsOnSuccessReason()
    {
        // Arrange
        var result = Result
            .Ok()
            .WithSuccess(
                new Success("created").WithMetadata(ResultExtensions.StatusCodeName, (int)HttpStatusCode.Created)
            );

        // Act
        var hasStatusCode = result.HasStatusCode((int)HttpStatusCode.Created);

        // Assert
        hasStatusCode.ShouldBeTrue();
    }

    [Test]
    public void ShouldNotFindStatusCode_WhenStatusCodeExistsOnlyOnNestedReason()
    {
        // Arrange
        var nestedReason = new Error("nested").WithMetadata(
            ResultExtensions.StatusCodeName,
            HttpCodes.Status404NotFound
        );
        var result = Result.Fail(new Error("outer").CausedBy(nestedReason));

        // Act
        var hasStatusCode = result.HasStatusCode(HttpCodes.Status404NotFound);

        // Assert
        hasStatusCode.ShouldBeFalse();
    }

    [Test]
    [Arguments(HttpStatusCode.NoContent)]
    [Arguments(HttpStatusCode.Forbidden)]
    [Arguments(HttpStatusCode.InternalServerError)]
    [Arguments(HttpStatusCode.ServiceUnavailable)]
    [Arguments(HttpStatusCode.GatewayTimeout)]
    public void ShouldMapKnownHttpStatusCodesForResult(HttpStatusCode statusCode)
    {
        // Arrange
        var result = Result.Ok();

        // Act
        var mapped = result.AddStatusCode(statusCode);

        // Assert
        mapped.HasStatusCode((int)statusCode).ShouldBeTrue();
    }

    [Test]
    [Arguments(HttpStatusCode.NoContent)]
    [Arguments(HttpStatusCode.Forbidden)]
    [Arguments(HttpStatusCode.InternalServerError)]
    [Arguments(HttpStatusCode.ServiceUnavailable)]
    [Arguments(HttpStatusCode.GatewayTimeout)]
    public void ShouldMapKnownHttpStatusCodesForGenericResult(HttpStatusCode statusCode)
    {
        // Arrange
        var result = Result.Ok("ok");

        // Act
        var mapped = result.AddStatusCode(statusCode);

        // Assert
        mapped.HasStatusCode((int)statusCode).ShouldBeTrue();
    }

    [Test]
    public void ShouldFallbackToRawStatusCode_WhenNoSpecialMappingExists()
    {
        // Arrange
        var result = Result.Fail("failed");

        // Act
        var mapped = result.AddStatusCode(HttpStatusCode.ServiceUnavailable, "service unavailable");

        // Assert
        mapped.HasStatusCode((int)HttpStatusCode.ServiceUnavailable).ShouldBeTrue();
    }

    [Test]
    public void ShouldMapTooManyRequestsStatusCode()
    {
        // Arrange
        var result = Result.Fail("failed");

        // Act
        var mapped = result.AddStatusCode(HttpStatusCode.TooManyRequests, "too many requests");

        // Assert
        mapped.HasStatusCode((int)HttpStatusCode.TooManyRequests).ShouldBeTrue();
        mapped.Has429TooManyRequestsError().ShouldBeTrue();
    }

    [Test]
    public void ShouldMapInternalServerErrorStatusCode()
    {
        // Arrange
        var result = Result.Fail("failed");

        // Act
        var mapped = result.AddStatusCode(HttpStatusCode.InternalServerError, "server error");

        // Assert
        mapped.Has500InternalServerError().ShouldBeTrue();
    }

    [Test]
    public void ShouldMapServiceUnavailableStatusCode()
    {
        // Arrange
        var result = Result.Fail("failed");

        // Act
        var mapped = result.AddStatusCode(HttpStatusCode.ServiceUnavailable, "unavailable");

        // Assert
        mapped.Has503ServiceUnavailableError().ShouldBeTrue();
    }

    [Test]
    public void ShouldMapInternalServerErrorStatusCodeForGenericResult()
    {
        // Arrange
        var result = Result.Fail<string>("failed");

        // Act
        var mapped = result.AddStatusCode(HttpStatusCode.InternalServerError, "server error");

        // Assert
        mapped.Has500InternalServerError().ShouldBeTrue();
    }

    [Test]
    public void ShouldMapServiceUnavailableStatusCodeForGenericResult()
    {
        // Arrange
        var result = Result.Fail<string>("failed");

        // Act
        var mapped = result.AddStatusCode(HttpStatusCode.ServiceUnavailable, "unavailable");

        // Assert
        mapped.Has503ServiceUnavailableError().ShouldBeTrue();
    }

    [Test]
    public void ShouldMapTooManyRequestsStatusCodeForGenericResult()
    {
        // Arrange
        var result = Result.Fail<string>("failed");

        // Act
        var mapped = result.AddStatusCode(HttpStatusCode.TooManyRequests, "too many requests");

        // Assert
        mapped.Has429TooManyRequestsError().ShouldBeTrue();
    }

    [Test]
    [Arguments(HttpStatusCode.RequestTimeout)]
    [Arguments(HttpStatusCode.InternalServerError)]
    [Arguments(HttpStatusCode.BadGateway)]
    [Arguments(HttpStatusCode.ServiceUnavailable)]
    [Arguments(HttpStatusCode.GatewayTimeout)]
    public void ShouldIdentifyServerUnreachableStatuses(HttpStatusCode statusCode)
    {
        // Arrange
        var result = Result.Fail("failed").AddStatusCode(statusCode);

        // Act
        var isServerUnreachable = result.IsServerUnreachable();

        // Assert
        isServerUnreachable.ShouldBeTrue();
    }

    [Test]
    public void ShouldIdentifyServerUnreachableStatusWhenHttpClientTimeout()
    {
        // Arrange
        var result = Result.Fail(
            new ExceptionalError(
                new TaskCanceledException(
                    "The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing."
                )
            )
        );

        // Act
        var isServerUnreachable = result.IsServerUnreachable();

        // Assert
        isServerUnreachable.ShouldBeTrue();
    }

    [Test]
    public void ShouldNotIdentifyClientErrorsAsServerUnreachable()
    {
        // Arrange
        var result = Result.Fail("failed").AddStatusCode(HttpStatusCode.NotFound);

        // Act
        var isServerUnreachable = result.IsServerUnreachable();

        // Assert
        isServerUnreachable.ShouldBeFalse();
    }

    [Test]
    [Arguments(HttpCodes.Status401Unauthorized)]
    [Arguments(HttpCodes.Status601PlexAuthenticationFailed)]
    [Arguments((int)HttpStatusCode.Conflict)]
    public void ShouldPreserveHttpStatus_WhenConvertingNonGenericFailureToDto(int statusCode)
    {
        // Arrange
        var result = Result.Fail("Access denied").AddStatusCode((HttpStatusCode)statusCode, "Access denied");

        // Act
        var dto = result.ToResultDTO();

        // Assert
        dto.IsSuccess.ShouldBeFalse();
        dto.StatusCode.ShouldBe(statusCode);
        dto.Errors.Count.ShouldBe(1);
        dto.Errors[0].Message.ShouldBe("Access denied");
        dto.Errors[0].Metadata[ResultExtensions.StatusCodeName].ShouldBe(statusCode);
        dto.Successes.ShouldBeEmpty();
    }

    [Test]
    [Arguments(HttpCodes.Status401Unauthorized)]
    [Arguments(HttpCodes.Status601PlexAuthenticationFailed)]
    [Arguments((int)HttpStatusCode.Conflict)]
    public void ShouldPreserveHttpStatus_WhenConvertingGenericFailureToDto(int statusCode)
    {
        // Arrange
        var result = new Result<string>().AddStatusCode((HttpStatusCode)statusCode, "Access denied");

        // Act
        var dto = result.ToResultDTO();

        // Assert
        dto.IsSuccess.ShouldBeFalse();
        dto.StatusCode.ShouldBe(statusCode);
        dto.Value.ShouldBeNull();
        dto.Errors.Count.ShouldBe(1);
        dto.Errors[0].Message.ShouldBe("Access denied");
        dto.Errors[0].Metadata[ResultExtensions.StatusCodeName].ShouldBe(statusCode);
        dto.Successes.ShouldBeEmpty();
    }

    [Test]
    [Arguments(HttpCodes.Status401Unauthorized)]
    [Arguments(HttpCodes.Status601PlexAuthenticationFailed)]
    [Arguments((int)HttpStatusCode.Conflict)]
    public void ShouldPreserveHttpStatus_WhenConvertingMappedFailureToDto(int statusCode)
    {
        // Arrange
        var result = new Result<string>().AddStatusCode((HttpStatusCode)statusCode, "Access denied");

        // Act
        var dto = result.ToResultDTO(value => value.ToUpperInvariant());

        // Assert
        dto.IsSuccess.ShouldBeFalse();
        dto.StatusCode.ShouldBe(statusCode);
        dto.Value.ShouldBeNull();
        dto.Errors.Count.ShouldBe(1);
        dto.Errors[0].Message.ShouldBe("Access denied");
        dto.Errors[0].Metadata[ResultExtensions.StatusCodeName].ShouldBe(statusCode);
        dto.Successes.ShouldBeEmpty();
    }
}
