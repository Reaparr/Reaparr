using System.Text.Json;

namespace Reaparr.PlexApi;

public sealed class PlexApiClient : IPlexApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PlexApiClientOptions _options;

    public PlexApiClient(HttpClient httpClient, PlexApiClientOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        if (options.ConnectionUrl != string.Empty)
            _httpClient.BaseAddress = new Uri(options.ConnectionUrl);
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        _httpClient.DefaultRequestHeaders.Accept.Add(ContentType.ApplicationJsonHeaderValue);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        request.SetRetryCount(_options.RetryCount);
        request.SetRetryProgressCallback(_options.RetryProgressAction);
        request.SetResponseBufferingTimeout(TimeSpan.FromSeconds(Math.Max(_options.Timeout, 1)));
        var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseContentRead, _options.CancellationToken);
        if (!response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == ContentType.TextHtml)
        {
            var originalContent = response.Content;
            response.Content = JsonSerializer.Serialize(
                new
                {
                    errors = new[]
                    {
                        new PlexError(response.ReasonPhrase ?? "Unknown Reason")
                        {
                            Code = (int)response.StatusCode,
                            Status = (int)response.StatusCode,
                        },
                    },
                },
                DefaultJsonSerializerOptions.ConfigStandard).ToStringContent();
            originalContent.Dispose();
        }
        return response;
    }

    public async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };
        if (request.Content is not null)
        {
            clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(_options.CancellationToken));
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var option in request.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        return clone;
    }

    public void Dispose() => _httpClient.Dispose();
}
