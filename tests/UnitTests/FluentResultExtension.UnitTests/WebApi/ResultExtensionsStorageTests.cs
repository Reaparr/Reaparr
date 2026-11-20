namespace Reaparr.FluentResultExtension.UnitTests;

public class ResultExtensionsStorageTests
{
    [Test]
    public void ShouldNotIdentifyHttpIOExceptionAsStorageError_WhenExceptionIsDirect()
    {
        // Arrange
        var result = Result.Fail(
            new ExceptionalError(
                new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.", null!)
            )
        );

        // Act
        var hasStorageError = result.HasStorageError();

        // Assert
        hasStorageError.ShouldBeFalse();
    }

    [Test]
    public void ShouldNotIdentifyHttpIOExceptionAsStorageError_WhenNestedInAnotherError()
    {
        // Arrange
        var result = Result.Fail(
            new Error("HTTP response failed").CausedBy(
                new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.", null!)
            )
        );

        // Act
        var hasStorageError = result.HasStorageError();

        // Assert
        hasStorageError.ShouldBeFalse();
    }

    [Test]
    public void ShouldIdentifyIOExceptionAsStorageError_WhenExceptionIsDirect()
    {
        // Arrange
        var result = Result.Fail(new ExceptionalError(new IOException("Disk write failed.")));

        // Act
        var hasStorageError = result.HasStorageError();

        // Assert
        hasStorageError.ShouldBeTrue();
    }

    [Test]
    public void ShouldIdentifyUnauthorizedAccessAsStorageError_WhenExceptionIsDirect()
    {
        // Arrange
        var result = Result.Fail(new ExceptionalError(new UnauthorizedAccessException("Access denied.")));

        // Act
        var hasStorageError = result.HasStorageError();

        // Assert
        hasStorageError.ShouldBeTrue();
    }

    [Test]
    public void ShouldIdentifyStorageError_WhenHttpIOExceptionAndIOExceptionAreNestedTogether()
    {
        // Arrange
        var result = Result.Fail(
            new Error("Download failed").CausedBy(
                new[]
                {
                    new ExceptionalError(
                        new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.", null!)
                    ),
                    new ExceptionalError(new IOException("Disk write failed.")),
                }
            )
        );

        // Act
        var hasStorageError = result.HasStorageError();

        // Assert
        hasStorageError.ShouldBeTrue();
    }
}
