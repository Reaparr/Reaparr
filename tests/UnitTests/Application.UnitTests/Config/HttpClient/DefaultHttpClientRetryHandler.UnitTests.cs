using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Reaparr.Application.UnitTests;

public class DefaultHttpClientRetryHandlerUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldRetryTruncatedBufferedGetAndReturnExactPayload()
    {
        // Arrange
        var truncatedContent = new TruncatedContent();
        var terminal = new SequenceTerminalHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = truncatedContent },
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("exact payload") }
        );
        using var client = CreateClient(terminal);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://plex.test/library");
        request.SetRetryCount(1);
        request.SetResponseBufferingTimeout(TimeSpan.FromSeconds(1));

        // Act
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var payload = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        payload.ShouldBe("exact payload");
        terminal.RequestCount.ShouldBe(2);
        truncatedContent.IsDisposed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldTimeoutSlowBufferedBody_WhenBufferingTimeoutExpires()
    {
        // Arrange
        var bufferingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bufferingCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminal = new SequenceTerminalHandler(() =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new SlowContent(bufferingStarted, bufferingCancelled),
            }
        );
        using var client = CreateClient(terminal);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://plex.test/slow");
        request.SetRetryCount(0);
        request.SetResponseBufferingTimeout(TimeSpan.FromSeconds(1));

        // Act
        var sendTask = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await bufferingStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Should.ThrowAsync<Polly.Timeout.TimeoutRejectedException>(() => sendTask);
        await bufferingCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        terminal.RequestCount.ShouldBe(1);
    }

    [Test]
    public async Task ShouldStopBufferingWhenCallerCancels()
    {
        // Arrange
        var bufferingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bufferingCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminal = new SequenceTerminalHandler(() =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new SlowContent(bufferingStarted, bufferingCancelled),
            }
        );
        using var client = CreateClient(terminal);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://plex.test/cancel");
        request.SetRetryCount(0);
        request.SetResponseBufferingTimeout(TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource();

        // Act
        var sendTask = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        await bufferingStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => sendTask);
        await bufferingCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        terminal.RequestCount.ShouldBe(1);
    }

    [Test]
    public async Task ShouldNotRetryUnsafePost_WhenBufferedSdkRequestFails()
    {
        // Arrange
        var terminal = new SequenceTerminalHandler(
            () =>
                new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("post failed"),
                },
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("unexpected retry") }
        );
        using var client = CreateClient(terminal);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://plex.test/unsafe");
        request.Content = new StringContent("request body");
        request.SetRetryCount(2);
        request.SetResponseBufferingTimeout(TimeSpan.FromSeconds(1));

        // Act
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var payload = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        payload.ShouldBe("post failed");
        terminal.RequestCount.ShouldBe(1);
    }

    [Test]
    public async Task ShouldLeaveDefaultResponseBodyUnbuffered_WhenNoBufferingTimeoutIsConfigured()
    {
        // Arrange
        var content = new TrackingContent("streamed payload");
        var terminal = new SequenceTerminalHandler(() =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
        );
        using var client = CreateClient(terminal);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://plex.test/stream");

        // Act
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.WasRead.ShouldBeFalse();
        terminal.RequestCount.ShouldBe(1);

        var payload = await response.Content.ReadAsStringAsync();
        payload.ShouldBe("streamed payload");
        content.WasRead.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldIsolateProgressCallbacks_WhenCachedPipelineIsReusedByTwoClients()
    {
        // Arrange
        var terminal = new PerHostTransientTerminalHandler();
        using var clientA = CreateClient(terminal);
        using var clientB = CreateClient(terminal);
        var progressA = new ConcurrentQueue<HttpRequestRetryProgress>();
        var progressB = new ConcurrentQueue<HttpRequestRetryProgress>();
        using var requestA = CreateBufferedRequest("http://plex-a.test/identity", progressA.Enqueue);
        using var requestB = CreateBufferedRequest("http://plex-b.test/identity", progressB.Enqueue);

        // Act
        var responses = await Task.WhenAll(
            clientA.SendAsync(requestA, HttpCompletionOption.ResponseHeadersRead),
            clientB.SendAsync(requestB, HttpCompletionOption.ResponseHeadersRead)
        );
        foreach (var response in responses)
            response.Dispose();

        // Assert
        terminal.RequestCount.ShouldBe(4);
        progressA.Count.ShouldBe(2);
        progressB.Count.ShouldBe(2);
        progressA.ShouldAllBe(progress =>
            progress.RequestUri.StartsWith("http://plex-a.test/", StringComparison.Ordinal)
        );
        progressB.ShouldAllBe(progress =>
            progress.RequestUri.StartsWith("http://plex-b.test/", StringComparison.Ordinal)
        );
        progressA.ShouldContain(progress => progress.Completed);
        progressA.ShouldContain(progress => !progress.Completed);
        progressB.ShouldContain(progress => progress.Completed);
        progressB.ShouldContain(progress => !progress.Completed);
    }

    private HttpClient CreateClient(HttpMessageHandler terminal)
    {
        var retryHandler = new DefaultHttpClientRetryHandler(Log) { InnerHandler = terminal };
        return new HttpClient(retryHandler);
    }

    private static HttpRequestMessage CreateBufferedRequest(
        string uri,
        Action<HttpRequestRetryProgress> progressCallback
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.SetRetryCount(1);
        request.SetResponseBufferingTimeout(TimeSpan.FromSeconds(1));
        request.SetRetryProgressCallback(progressCallback);
        return request;
    }

    private sealed class SequenceTerminalHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _requestCount;
        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var index = Interlocked.Increment(ref _requestCount) - 1;
            var response = responses[Math.Min(index, responses.Length - 1)]();
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private sealed class PerHostTransientTerminalHandler : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, int> _requestCounts = new();

        public int RequestCount => _requestCounts.Values.Sum();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var host = request.RequestUri!.Host;
            var requestCount = _requestCounts.AddOrUpdate(host, 1, static (_, count) => count + 1);
            var statusCode = requestCount == 1 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(statusCode == HttpStatusCode.OK ? "success" : "retry"),
                RequestMessage = request,
            };
            return Task.FromResult(response);
        }
    }

    private sealed class TruncatedContent : HttpContent
    {
        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken
        )
        {
            await stream.WriteAsync("partial payload"u8.ToArray(), cancellationToken);
            throw new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = disposing;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingContent(string payload) : HttpContent
    {
        private readonly byte[] _payload = Encoding.UTF8.GetBytes(payload);

        public bool WasRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken
        )
        {
            WasRead = true;
            await stream.WriteAsync(_payload, cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _payload.Length;
            return true;
        }
    }

    private sealed class SlowContent(TaskCompletionSource bufferingStarted, TaskCompletionSource bufferingCancelled)
        : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken
        )
        {
            bufferingStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                bufferingCancelled.TrySetResult();
                throw;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}
