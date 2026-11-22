using Downloader;
using Reaparr.Settings.Contracts;

namespace Reaparr.BaseTests;

public class UnitTestDataConfig : BaseConfig<UnitTestDataConfig>
{
    public Action<FakeDataConfig>? DatabaseOptions { get; set; } = null;

    public IConfigManager? MockConfigManager { get; set; }

    public int DownloadSpeedLimitInKib { get; set; }

    public int PlexServerSettingsCount { get; set; } = 5;

    public Action<Mock<HttpMessageHandler>, IReaparrDbContext>? HttpClientOptions { get; set; }

    /// <summary>
    /// Set the mock option to create a mock PlexApi Server with the given configuration.
    /// </summary>
    public Action<PlexApiDataConfig>? BaseMockHttpClientOptions { get; set; }

    public Action<IFileSystem, IReaparrDbContext>? FileSystemOptions { get; set; }

    public Func<IFile, Func<DownloadConfiguration, IDownloadService>> MockDownloadServiceFactory { get; set; } =
        MockDownloadService.SuccessFactory;

    /// <summary>
    /// Optional per-test Autofac overrides. Invoked after TestModule registration so last registration wins.
    /// </summary>
    public Action<ContainerBuilder>? OverrideServices { get; set; }

    public IAppBuildInfo? OverrideAppBuildInfo { get; set; }

    public Action<MockAppRuntimeInfo>? OverrideAppRuntimeInfo { get; set; }
}
