using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class PlexApiClientFactoryExecutionUnitTests : BaseUnitTest
{
    private readonly PlexApiHttpClientFixture _httpClientFixture;

    public PlexApiClientFactoryExecutionUnitTests()
    {
        _httpClientFixture = new PlexApiHttpClientFixture(Log);
    }

    [After(Test)]
    public void CleanupHttpClientFixture() => _httpClientFixture.Dispose();

    [Test]
    public async Task ShouldMapPollyTimeoutTo408AndPreserveException()
    {
        // Arrange
        var exception = new Polly.Timeout.TimeoutRejectedException("attempt timeout");

        // Act
        var result = await Task.FromException<object>(exception).ToResponse(CancellationToken.None);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Has408RequestTimeout().ShouldBeTrue();
        result.Errors.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
    }

    [Test]
    public async Task ShouldReturnSuccessfulSdkResponseAndStatusMetadata_WhenIdentityEndpointSucceeds()
    {
        // Arrange
        var handler = new SequenceHandler(_ => CreateIdentityResponse(HttpStatusCode.OK));
        var factory = CreateFactory(handler);

        // Act
        var client = factory.CreateClient(
            "token-a",
            new PlexApiClientOptions { ConnectionUrl = "http://plex.test", RetryCount = 0 },
            CancellationToken
        );
        var result = await client.General.GetIdentityAsync().ToResponse(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull();
        result.Value.StatusCode.ShouldBe((int)HttpStatusCode.OK);
        result.Value.Object.ShouldNotBeNull();
        handler.Requests.Count.ShouldBe(1);
        handler.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/identity");
    }

    [Test]
    public async Task ShouldReplaySafeGet_WhenSdkBodyIsTruncated()
    {
        // Arrange
        var handler = new SequenceHandler(index =>
            index == 0
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new TruncatedJsonContent() }
                : CreateIdentityResponse(HttpStatusCode.OK)
        );
        var factory = CreateFactory(handler);

        // Act
        var client = factory.CreateClient(
            "token-a",
            new PlexApiClientOptions { ConnectionUrl = "http://plex.test", RetryCount = 1 },
            CancellationToken
        );
        var result = await client.General.GetIdentityAsync().ToResponse(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.StatusCode.ShouldBe((int)HttpStatusCode.OK);
        handler.Requests.Count.ShouldBe(2);
    }

    [Test]
    public async Task ShouldNotReplayUnsafeOperation_WhenOperationFails()
    {
        // Arrange
        var handler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{\"error\":\"offline\"}", Encoding.UTF8, "application/json"),
        });
        var factory = CreateFactory(handler);

        // Act
        var client = factory.CreateClient(
            "token-a",
            new PlexApiClientOptions { ConnectionUrl = "http://plex.test", RetryCount = 0 },
            CancellationToken
        );
        var result = await client.Authentication.CreateLegacyPinAsync("http://plex.test").ToResponse(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(3);
        result
            .GetStatusCodeReason()!
            .Metadata[ResultExtensions.StatusCodeName]
            .ShouldBe((int)HttpStatusCode.ServiceUnavailable);
        handler.Requests.Count.ShouldBe(1);
        handler.Requests.Single().Method.ShouldBe(HttpMethod.Post);
    }

    [Test]
    public async Task ShouldRetrySlowBodyAttempt_ThenReturnSuccess()
    {
        // Arrange
        var bodyCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new SequenceHandler(index =>
            index == 0
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new SlowBodyContent(bodyCancelled) }
                : CreateIdentityResponse(HttpStatusCode.OK)
        );
        var factory = CreateFactory(handler);

        // Act
        var client = factory.CreateClient(
            "token-a",
            new PlexApiClientOptions
            {
                ConnectionUrl = "http://plex.test",
                Timeout = 1,
                RetryCount = 1,
            },
            CancellationToken
        );
        var result = await client.General.GetIdentityAsync().ToResponse(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        handler.Requests.Count.ShouldBe(2);
        await bodyCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task ShouldNotReplayGet_WhenRetryCountIsZero()
    {
        // Arrange
        var handler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("offline", Encoding.UTF8, "text/plain"),
        });
        var factory = CreateFactory(handler);

        // Act
        var client = factory.CreateClient(
            "token-a",
            new PlexApiClientOptions { ConnectionUrl = "http://plex.test", RetryCount = 0 },
            CancellationToken
        );
        var result = await client.General.GetIdentityAsync().ToResponse(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(3);
        handler.Requests.Count.ShouldBe(1);
    }

    [Test]
    public async Task ShouldPreserveOriginalException_WhenRetryBudgetIsExhausted()
    {
        // Arrange
        var handler = new SequenceHandler(_ => throw new HttpRequestException("sentinel transport failure"));
        var factory = CreateFactory(handler);

        // Act
        var client = factory.CreateClient(
            "token-a",
            new PlexApiClientOptions { ConnectionUrl = "http://plex.test", RetryCount = 2 },
            CancellationToken
        );
        var result = await client.General.GetIdentityAsync().ToResponse(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Errors.OfType<ExceptionalError>().Single().Exception.Message.ShouldContain("sentinel transport failure");
        handler.Requests.Count.ShouldBe(3);
    }

    [Test]
    public async Task ShouldStopAtTotalBudget_WhenSlowAttemptsConsumeTheBudget()
    {
        // Arrange
        var bodyCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new SlowBodyContent(bodyCancelled),
        });
        var factory = CreateFactory(handler);
        var started = Stopwatch.GetTimestamp();

        // Act
        var client = factory.CreateClient(
            "token-a",
            new PlexApiClientOptions
            {
                ConnectionUrl = "http://plex.test",
                Timeout = 1,
                RetryCount = 2,
            },
            CancellationToken
        );
        var result = await client.General.GetIdentityAsync().ToResponse(CancellationToken);

        // Assert
        result.Errors.Count.ShouldBe(2);
        result.IsSuccess.ShouldBeFalse();
        result.Has408RequestTimeout().ShouldBeTrue();
        handler.Requests.Count.ShouldBe(3);
        Stopwatch.GetElapsedTime(started).ShouldBeGreaterThan(TimeSpan.FromSeconds(3));
        await bodyCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task ShouldObserveOperationCancellationDuringBodyRead()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var bodyCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new CancellationObservingContent(bodyCancelled),
        });
        var factory = CreateFactory(handler);
        var client = factory.CreateClient(
            "token-a",
            new PlexApiClientOptions
            {
                ConnectionUrl = "http://plex.test",
                Timeout = 30,
                RetryCount = 0,
            },
            cancellation.Token
        );
        var operation = client.General.GetIdentityAsync().ToResponse(cancellation.Token);

        // Act
        await cancellation.CancelAsync();
        var result = await operation;

        result.IsCancelled.ShouldBeTrue();
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        handler.CancellationTokens.ShouldContain(token => token.CanBeCanceled);
        await bodyCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        handler.Requests.Count.ShouldBe(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldHonorEffectiveCancellationToken_WhenOptionsAndFactoryTokensDiffer(bool overrideToken)
    {
        // Arrange
        using var optionsCancellation = new CancellationTokenSource();
        using var argumentCancellation = new CancellationTokenSource();
        var bodyCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new CancellationObservingContent(bodyCancelled),
        });
        var options = new PlexApiClientOptions
        {
            ConnectionUrl = "http://plex.test",
            Timeout = 30,
            RetryCount = 0,
            CancellationToken = optionsCancellation.Token,
        };
        var factory = CreateFactory(handler);
        var client = overrideToken
            ? factory.CreateClient("token-a", options, argumentCancellation.Token)
            : factory.CreateClient("token-a", options);
        var effectiveCancellation = overrideToken ? argumentCancellation : optionsCancellation;
        var operation = client.General.GetIdentityAsync().ToResponse(effectiveCancellation.Token);

        // Act
        await effectiveCancellation.CancelAsync();
        var result = await operation.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        await bodyCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        handler.Requests.Count.ShouldBe(1);
        handler.CancellationTokens.Single().IsCancellationRequested.ShouldBeTrue();
        options.CancellationToken.ShouldBe(optionsCancellation.Token);
        if (overrideToken)
            optionsCancellation.IsCancellationRequested.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldKeepServerUrlsIsolated_WhenOperationsRunConcurrently()
    {
        var handler = new SequenceHandler(_ => CreateIdentityResponse(HttpStatusCode.OK));
        var factory = CreateFactory(handler);
        var progressA = new List<HttpRequestRetryProgress>();
        var progressB = new List<HttpRequestRetryProgress>();
        // Act
        var results = await Task.WhenAll(
            factory
                .CreateClient(
                    "token-a",
                    new PlexApiClientOptions
                    {
                        ConnectionUrl = "http://plex-a.test",
                        RetryCount = 0,
                        RetryProgressAction = progressA.Add,
                    },
                    CancellationToken
                )
                .General.GetIdentityAsync()
                .ToResponse(CancellationToken),
            factory
                .CreateClient(
                    "token-b",
                    new PlexApiClientOptions
                    {
                        ConnectionUrl = "http://plex-b.test",
                        RetryCount = 0,
                        RetryProgressAction = progressB.Add,
                    },
                    CancellationToken
                )
                .General.GetIdentityAsync()
                .ToResponse(CancellationToken)
        );

        // Assert
        results.ShouldAllBe(result => result.IsSuccess && result.Errors.Count == 0);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests.Select(request => request.RequestUri!.Host).Order().ShouldBe(["plex-a.test", "plex-b.test"]);
        progressA.Count.ShouldBe(1);
        progressA.Single().Completed.ShouldBeTrue();
        progressA.Single().RequestUri.ShouldBe("http://plex-a.test/identity");
        progressB.Count.ShouldBe(1);
        progressB.Single().Completed.ShouldBeTrue();
        progressB.Single().RequestUri.ShouldBe("http://plex-b.test/identity");
    }

    private PlexApiClientFactory CreateFactory(HttpMessageHandler handler)
    {
        return new PlexApiClientFactory(
            options => new PlexApiClient(_httpClientFixture.Create(handler), options!),
            Moq.Mock.Of<IReaparrDbContextFactory>()
        );
    }

    private static HttpResponseMessage CreateIdentityResponse(HttpStatusCode statusCode) =>
        FakePlexApiData.GetPlexServerIdentityResponse(statusCode, new Seed(9921)).RawResponse;

    private sealed class SequenceHandler(Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private int _requestCount;
        public ConcurrentBag<HttpRequestMessage> Requests { get; } = [];
        public ConcurrentBag<CancellationToken> CancellationTokens { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CancellationTokens.Add(cancellationToken);
            Requests.Add(request);
            var response = responseFactory(Interlocked.Increment(ref _requestCount) - 1);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private sealed class CancellationObservingContent(TaskCompletionSource cancelled) : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new CancellationStream(cancelled));

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.Delay(Timeout.InfiniteTimeSpan);

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

    private sealed class TruncatedJsonContent : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync("{\"MediaContainer\":"u8.ToArray());
            throw new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.");
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken
        )
        {
            await stream.WriteAsync("{\"MediaContainer\":"u8.ToArray(), cancellationToken);
            throw new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

    private sealed class SlowBodyContent(TaskCompletionSource cancelled) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

    private sealed class CancellationStream(TaskCompletionSource cancelled) : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 0;
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
        }
    }
}
