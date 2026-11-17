using Microsoft.Extensions.DependencyInjection;
using Reaparr.Application;

namespace Reaparr.PlexApi.UnitTests;

internal sealed class PlexApiHttpClientFixture(ILogger log) : IDisposable
{
    private readonly List<ServiceProvider> _serviceProviders = [];

    public HttpClient Create(HttpMessageHandler transport)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger>(log);
        services.RegisterDefaultHttpClient();
        services
            .AddHttpClient(HttpClientModule.DefaultClientName)
            .ConfigurePrimaryHttpMessageHandler(() => transport);

        var serviceProvider = services.BuildServiceProvider();
        _serviceProviders.Add(serviceProvider);
        return serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientModule.DefaultClientName);
    }

    public void Dispose()
    {
        foreach (var serviceProvider in _serviceProviders)
            serviceProvider.Dispose();

        _serviceProviders.Clear();
    }
}
