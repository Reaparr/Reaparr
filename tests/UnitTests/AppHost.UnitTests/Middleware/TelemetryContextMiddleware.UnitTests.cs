using System.Reactive.Disposables;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Reaparr.Application.Contracts;

namespace Reaparr.AppHost.UnitTests;

public class TelemetryContextMiddlewareUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldPreserveTheResponse_AndDisposeRequestContext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[TelemetryProtocol.SessionIdHeader] = "native-session-data";
        var disposed = false;
        var scope = Disposable.Create(() => disposed = true);
        Mock.Mock<ITelemetry>()
            .Setup(x => x.BeginRequest("native-session-data"))
            .Returns(scope)
            .Verifiable(Times.Once());
        var middleware = new TelemetryContextMiddleware(httpContext =>
        {
            disposed.ShouldBeFalse();
            httpContext.Response.StatusCode = StatusCodes.Status202Accepted;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, Mock.Mock<ITelemetry>().Object);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        disposed.ShouldBeTrue();
        Mock.Mock<ITelemetry>()
            .Verify(
                x => x.HttpRequestFailed(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Never()
            );
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldPreserveTheOriginalFailure_AndCaptureItsRouteWithoutQueryCredentials()
    {
        // Arrange
        var failure = new InvalidOperationException("Unexpected business failure");
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/work/42";
        context.Request.QueryString = new QueryString("?apiKey=must-not-be-context");
        context.SetEndpoint(
            new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse("/api/work/{id}"),
                0,
                EndpointMetadataCollection.Empty,
                "work"
            )
        );
        var disposed = false;
        Mock.Mock<ITelemetry>().Setup(x => x.BeginRequest(null)).Returns(Disposable.Create(() => disposed = true));
        Mock.Mock<ITelemetry>()
            .Setup(x => x.HttpRequestFailed(failure, HttpMethods.Post, "/api/work/{id}"))
            .Verifiable(Times.Once());
        var middleware = new TelemetryContextMiddleware(_ => Task.FromException(failure));

        // Act
        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(context, Mock.Mock<ITelemetry>().Object)
        );

        // Assert
        thrown.ShouldBeSameAs(failure);
        disposed.ShouldBeTrue();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldDistinguishRequestCancellation_FromUnexpectedTransportCancellation(bool requestCancelled)
    {
        // Arrange
        var cancellation = new OperationCanceledException("transport aborted");
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(requestCancelled) };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/work";
        var middleware = new TelemetryContextMiddleware(_ => Task.FromException(cancellation));

        // Act
        OperationCanceledException? thrown = null;
        try
        {
            await middleware.InvokeAsync(context, Mock.Mock<ITelemetry>().Object);
        }
        catch (OperationCanceledException exception)
        {
            thrown = exception;
        }

        // Assert
        thrown.ShouldBeSameAs(cancellation);
        Mock.Mock<ITelemetry>()
            .Verify(
                x => x.HttpRequestFailed(cancellation, HttpMethods.Get, "/api/work"),
                requestCancelled ? Times.Never() : Times.Once()
            );
    }
}
