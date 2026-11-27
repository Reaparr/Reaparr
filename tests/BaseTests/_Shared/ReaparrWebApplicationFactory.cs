using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reaparr.AppHost;

namespace Reaparr.BaseTests;

public class ReaparrWebApplicationFactory : WebApplicationFactory<Program>
{
    public Seed Seed { get; }

    public readonly string MemoryDbName;

    private static readonly ILogger _log = LogFactory.Create<ReaparrWebApplicationFactory>();

    private readonly UnitTestDataConfig _config;
    private readonly bool _useTestAuthentication;
    private readonly IAppBuildInfo _buildInfo;
    private readonly IAppRuntimeInfo _runtimeInfo;
    private IHost? _host;

    public ReaparrWebApplicationFactory(
        Seed seed,
        string memoryDbName,
        UnitTestDataConfig config,
        IAppBuildInfo buildInfo,
        IAppRuntimeInfo runtimeInfo,
        bool useTestAuthentication = true
    )
    {
        Seed = seed;
        MemoryDbName = memoryDbName;
        _config = config;
        _buildInfo = buildInfo;
        _runtimeInfo = runtimeInfo;
        _useTestAuthentication = useTestAuthentication;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Disable caching by using custom configurations
        builder.UseSetting("cacheEnabled", "false");

        builder.ConfigureTestServices(services =>
        {
            if (_useTestAuthentication)
            {
                // https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-9.0#mock-authentication
                services
                    .AddAuthentication(defaultScheme: "TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureContainer<ContainerBuilder>(autoFacBuilder =>
        {
            autoFacBuilder.RegisterModule(new TestModule
            {
                MemoryDbName = MemoryDbName,
                Config = _config,
                BuildInfo = _buildInfo,
                RuntimeInfo = _runtimeInfo,
            });
            // Allow per-test overrides last so they win.
            _config.OverrideServices?.Invoke(autoFacBuilder);
        });

        try
        {
            return _host = base.CreateHost(builder);
        }
        catch (Exception e)
        {
            _log.Here().Fatal(e, "Failed to create the test host");
            throw;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        catch
        {
            // The factory only disposes the host after StopAsync returns successfully.
            if (_host is IAsyncDisposable asyncHost)
            {
                var result = await Result.Try(asyncHost.DisposeAsync);
                result.LogIfFailed();
            }
            else if (_host is not null)
            {
                Result.Try(_host.Dispose).LogIfFailed();
            }

            throw;
        }
        finally
        {
            _host = null;
        }
    }
}
