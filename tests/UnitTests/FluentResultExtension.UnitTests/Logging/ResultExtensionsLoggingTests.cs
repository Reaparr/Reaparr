using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace Reaparr.FluentResultExtension.UnitTests;

public class ResultExtensionsLoggingTests : BaseUnitTest
{
    #region Setup/Teardown

    #endregion

    [Test]
    public void ShouldHaveDebugLoggedWithParameterName_WhenLogDebugIsCalled()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.IsNull(parameterName).LogDebug();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Debug);
        }
    }

    [Test]
    public void ShouldHaveDebugLoggedWithParameterName_WhenLogDebugIsCalledOnResultT()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.Create201CreatedResult(100, parameterName).LogDebug();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Debug);
        }
    }

    [Test]
    public void ShouldHaveErrorLoggedWithParameterName_WhenLogErrorIsCalled()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.IsNull(parameterName).LogError();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Error);
        }
    }

    [Test]
    public void ShouldHaveErrorLoggedWithParameterName_WhenLogErrorIsCalledOnResultT()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.Create201CreatedResult(100, parameterName).LogError();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Error);
        }
    }

    [Test]
    public void ShouldHaveExceptionErrorLogged_WhenResultHasExceptionalError()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var exceptionError = new ExceptionalError("Test Exceptional Error #1", new Exception("Test Exception #1"));
            var result = Result.Fail(exceptionError);

            // Act
            result.LogError();

            // Assert
            var logContext = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

            var logEvent = logContext.First();
            logEvent.Level.ShouldBe(LogEventLevel.Error);
            logEvent.MessageTemplate.Text.ShouldContain("Test Exceptional Error #1");
            logEvent.Exception.ShouldNotBeNull();
            logEvent.Exception.Message.ShouldBe("Test Exception #1");
        }
    }

    [Test]
    public void ShouldHaveFatalLoggedWithParameterName_WhenLogFatalIsCalled()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.IsNull(parameterName).LogFatal();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Fatal);
        }
    }

    [Test]
    public void ShouldHaveFatalLoggedWithParameterName_WhenLogFatalIsCalledOnResultT()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.Create201CreatedResult(100, parameterName).LogFatal();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Fatal);
        }
    }

    [Test]
    public void ShouldHaveInformationLoggedWithParameterName_WhenLogInformationIsCalled()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.IsNull(parameterName).LogInformation();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Information);
        }
    }

    [Test]
    public void ShouldHaveInformationLoggedWithParameterName_WhenLogInformationIsCalledOnResultT()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.Create201CreatedResult(100, parameterName).LogInformation();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Information);
        }
    }

    [Test]
    public void ShouldHaveMultipleErrorsLogged_WhenResultHasMultipleErrors()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var result = Result.Fail("Test Error #1").WithError("Test Error #2").WithError("Test Error #3");

            // Act
            result.LogError();

            // Assert
            var logContext = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

            for (var i = 0; i < logContext.Count; i++)
            {
                logContext[i].Level.ShouldBe(LogEventLevel.Error);
                logContext[i].MessageTemplate.Text.ShouldContain($"Test Error #{i + 1}");
            }
        }
    }

    [Test]
    public void ShouldHaveMultipleNestedErrorsAndMetadataLogged_WhenResultHasMultipleNestedErrorsAndMetadata()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var numberOfErrors = 5;
            var numberOfMetadata = 5;
            var result = Result.Fail("Test Error #1");

            for (var i = 1; i <= numberOfErrors; i++)
            {
                var error = new Error($"Nested Error #{i}");

                for (var j = 1; j <= numberOfMetadata; j++)
                    error.WithMetadata($"Key{j}", $"Value{j}");

                result.Errors.First().Reasons.Add(error);
            }

            // Act
            result.LogError();

            // Assert
            var logContext = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

            var logEvent = logContext.First();
            logEvent.Level.ShouldBe(LogEventLevel.Error);
            logEvent.MessageTemplate.Text.ShouldContain("Test Error #1");

            for (var i = 1; i <= numberOfErrors; i++)
            {
                logEvent.MessageTemplate.Text.ShouldContain($"Nested Error #{i}");
                for (var j = 1; j <= numberOfMetadata; j++)
                    logEvent.MessageTemplate.Text.ShouldContain($"Key{j} - Value{j}");
            }
        }
    }

    [Test]
    public void ShouldHaveMultipleNestedErrorsLogged_WhenResultHasMultipleNestedErrors()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var result = Result.Fail("Test Error #1");

            result.AddNestedErrors([
                new Error("Error #1"),
                new Error("Error #2"),
                new Error("Error #3"),
                new Error("Error #4"),
                new Error("Error #5"),
            ]);

            // Act
            result.LogError();

            // Assert
            var logContext = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

            logContext.First().Level.ShouldBe(LogEventLevel.Error);
            logContext.First().MessageTemplate.Text.ShouldContain("Test Error #1");

            for (var i = 1; i < logContext.Count; i++)
            {
                logContext[i].Level.ShouldBe(LogEventLevel.Error);
                logContext[i].MessageTemplate.Text.ShouldContain($"--Error #{i}");
            }
        }
    }

    [Test]
    public void ShouldHaveMultipleNestedMetadataLogged_WhenResultHasMultipleNestedMetadata()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var result = Result.Fail("Test Error #1");
            for (var i = 1; i < 5; i++)
                result.Errors.First().Metadata.Add($"Key{i}", $"Value{i}");

            // Act
            result.LogError();

            // Assert
            var logContext = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

            var logEvent = logContext.First();
            logEvent.Level.ShouldBe(LogEventLevel.Error);
            logEvent.MessageTemplate.Text.ShouldContain("Test Error #1");

            for (var i = 1; i < 5; i++)
                logEvent.MessageTemplate.Text.ShouldContain($"Key{i} - Value{i}");
        }
    }

    [Test]
    public void ShouldHaveVerboseLoggedWithParameterName_WhenLogVerboseIsCalled()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.IsNull(parameterName).LogVerbose();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Verbose);
        }
    }

    [Test]
    public void ShouldHaveVerboseLoggedWithParameterName_WhenLogVerboseIsCalledOnResultT()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.Create201CreatedResult(100, parameterName).LogVerbose();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Verbose);
        }
    }

    [Test]
    public void ShouldHaveWarningLoggedWithParameterName_WhenLogWarningIsCalled()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.IsNull(parameterName).LogWarning();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Warning);
        }
    }

    [Test]
    public void ShouldHaveWarningLoggedWithParameterName_WhenLogWarningIsCalledOnResultT()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var parameterName = "ParameterXYZ";

            // Act
            ResultExtensions.Create201CreatedResult(100, parameterName).LogWarning();

            // Assert
            var logEvent = TestCorrelator.GetLogEventsFromCurrentContext().First();
            logEvent.MessageTemplate.Text.ShouldContain(parameterName);
            logEvent.Level.ShouldBe(LogEventLevel.Warning);
        }
    }
}
