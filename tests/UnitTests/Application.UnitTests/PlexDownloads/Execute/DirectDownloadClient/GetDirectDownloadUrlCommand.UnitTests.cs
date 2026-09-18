using System.Net;

namespace Reaparr.Application.UnitTests;

public class GetDirectDownloadUrlCommandUnitTests : BaseUnitTest<GetDirectDownloadUrlCommandHandler>
{
    [Test]
    public async Task ShouldReturnUrlWithoutDownloadQuery_WhenInitialProbeSucceeds()
    {
        // Arrange
        await SetupDatabase(
            90201,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        SetupHttpClientFactory(HttpStatusCode.OK);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(
            new GetDirectDownloadUrlCommand(downloadTask.PlexServerId, downloadTask.FileLocationUrl),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotContain("download=1");
        Mock.Mock<IHttpClientFactory>().Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once());
    }

    [Test]
    public async Task ShouldAppendDownloadQuery_WhenInitialProbeIsForbidden()
    {
        // Arrange
        await SetupDatabase(
            90202,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        SetupHttpClientFactory(HttpStatusCode.Forbidden, HttpStatusCode.OK);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(
            new GetDirectDownloadUrlCommand(downloadTask.PlexServerId, downloadTask.FileLocationUrl),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("download=1");
        Mock.Mock<IHttpClientFactory>().Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenForbiddenAndFallbackAlsoFails()
    {
        // Arrange
        await SetupDatabase(
            90203,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        SetupHttpClientFactory(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(
            new GetDirectDownloadUrlCommand(downloadTask.PlexServerId, downloadTask.FileLocationUrl),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IHttpClientFactory>().Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once());
    }

    [Test]
    public async Task ShouldAdd404NotFoundError_WhenInitialProbeReturnsNotFound()
    {
        // Arrange
        await SetupDatabase(
            90207,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        SetupHttpClientFactory(HttpStatusCode.NotFound, HttpStatusCode.NotFound);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(
            new GetDirectDownloadUrlCommand(downloadTask.PlexServerId, downloadTask.FileLocationUrl),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Has404NotFoundError().ShouldBeTrue();
        result.ToResult().FindStatusCode().ShouldBe((int)HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ShouldAdd404NotFoundError_WhenFallbackProbeReturnsNotFound()
    {
        // Arrange
        await SetupDatabase(
            90208,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        SetupHttpClientFactory(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(
            new GetDirectDownloadUrlCommand(downloadTask.PlexServerId, downloadTask.FileLocationUrl),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Has404NotFoundError().ShouldBeTrue();
        result.ToResult().FindStatusCode().ShouldBe((int)HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ShouldRetryTransientProbeFailuresAndReturnUrl_WhenProbeEventuallySucceeds()
    {
        // Arrange
        await SetupDatabase(
            90204,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var handler = new DefaultProbeTransientSequenceHandler(
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK
        );
        var retryHandler = new DefaultHttpClientRetryHandler(new LoggerConfiguration().CreateLogger())
        {
            InnerHandler = handler,
        };
        var httpClient = new HttpClient(retryHandler);
        Mock.Mock<IHttpClientFactory>().Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(
            new GetDirectDownloadUrlCommand(downloadTask.PlexServerId, downloadTask.FileLocationUrl),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        handler.DefaultProbeRequestCount.ShouldBe(3);
        Mock.Mock<IHttpClientFactory>().Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once());
    }

    [Test]
    public async Task ShouldReturnFailedResult_AfterConfiguredTransientRetriesAreExhausted()
    {
        // Arrange
        await SetupDatabase(
            90205,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var handler = new ExhaustedTransientRetryHandler();
        var retryHandler = new DefaultHttpClientRetryHandler(new LoggerConfiguration().CreateLogger())
        {
            InnerHandler = handler,
        };
        var httpClient = new HttpClient(retryHandler);
        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient)
            .Verifiable(Times.Once());

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(
            new GetDirectDownloadUrlCommand(downloadTask.PlexServerId, downloadTask.FileLocationUrl),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        handler.DefaultProbeRequestCount.ShouldBe(4);
        handler.FallbackProbeRequestCount.ShouldBe(1);
        Mock.Mock<IHttpClientFactory>().Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once());
    }

    [Test]
    public async Task ShouldStartDefaultAndFallbackProbeRequestsInParallel()
    {
        // Arrange
        await SetupDatabase(
            90206,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var handler = new ParallelProbeHandler();
        var retryHandler = new DefaultHttpClientRetryHandler(new LoggerConfiguration().CreateLogger())
        {
            InnerHandler = handler,
        };
        var httpClient = new HttpClient(retryHandler);
        Mock.Mock<IHttpClientFactory>().Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var sut = CreateSut();

        // Act
        var executeTask = sut.ExecuteAsync(
            new GetDirectDownloadUrlCommand(downloadTask.PlexServerId, downloadTask.FileLocationUrl),
            CancellationToken
        );

        await Task.WhenAll(
            handler.DefaultProbeStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken),
            handler.FallbackProbeStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken)
        );

        handler.DefaultProbeResult.SetResult(HttpStatusCode.Forbidden);
        handler.FallbackProbeResult.SetResult(HttpStatusCode.OK);

        var result = await executeTask;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("download=1");
    }

    private GetDirectDownloadUrlCommandHandler CreateSut() =>
        Mock.Create<GetDirectDownloadUrlCommandHandler>(new TypedParameter(typeof(IReaparrDbContext), IDbContext));

    private void SetupHttpClientFactory(params HttpStatusCode[] statuses)
    {
        var handler = new SequenceStatusCodeHandler(statuses);
        var retryHandler = new DefaultHttpClientRetryHandler(new LoggerConfiguration().CreateLogger())
        {
            InnerHandler = handler,
        };
        var httpClient = new HttpClient(retryHandler);
        Mock.Mock<IHttpClientFactory>().Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    private sealed class SequenceStatusCodeHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestCount++;

            if (_statuses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(SequenceStatusCodeHandler)} was exhausted while probing {request.RequestUri}."
                );
            }

            var statusCode = _statuses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(statusCode) { RequestMessage = request });
        }
    }

    private sealed class ParallelProbeHandler : HttpMessageHandler
    {
        public TaskCompletionSource DefaultProbeStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FallbackProbeStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpStatusCode> DefaultProbeResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpStatusCode> FallbackProbeResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var hasDownloadFlag = request.RequestUri?.Query.Contains("download=1", StringComparison.Ordinal) == true;

            if (hasDownloadFlag)
            {
                FallbackProbeStarted.TrySetResult();
                var statusCode = await FallbackProbeResult.Task.WaitAsync(cancellationToken);
                return new HttpResponseMessage(statusCode) { RequestMessage = request };
            }

            DefaultProbeStarted.TrySetResult();
            var defaultStatusCode = await DefaultProbeResult.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(defaultStatusCode) { RequestMessage = request };
        }
    }

    private sealed class ExhaustedTransientRetryHandler : HttpMessageHandler
    {
        public int DefaultProbeRequestCount { get; private set; }

        public int FallbackProbeRequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var hasDownloadFlag = request.RequestUri?.Query.Contains("download=1", StringComparison.Ordinal) == true;

            if (hasDownloadFlag)
            {
                FallbackProbeRequestCount++;

                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

                throw new InvalidOperationException("Fallback probe should be cancelled before completing.");
            }

            DefaultProbeRequestCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = request };
        }
    }

    private sealed class DefaultProbeTransientSequenceHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _defaultProbeStatuses = new(statuses);

        public int DefaultProbeRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var hasDownloadFlag = request.RequestUri?.Query.Contains("download=1", StringComparison.Ordinal) == true;

            if (hasDownloadFlag)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { RequestMessage = request });
            }

            DefaultProbeRequestCount++;

            if (_defaultProbeStatuses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(DefaultProbeTransientSequenceHandler)} was exhausted while probing {request.RequestUri}."
                );
            }

            var statusCode = _defaultProbeStatuses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(statusCode) { RequestMessage = request });
        }
    }
}
