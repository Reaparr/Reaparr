namespace Reaparr.BaseTests;

public static class GlobalTestHooks
{
    [Before(TestSession)]
    public static async Task SetupTestSession()
    {
        var appRuntimeInfo = new MockAppRuntimeInfo { IsUnmasked = true };
        var pathProvider = new MockPathProvider(MockDatabase.GetMemoryDatabaseName());
        var testLogConfig = new TestLogConfig(appRuntimeInfo, pathProvider, new MockAppBuildInfo());

        LogFactory.SetupLogging(testLogConfig, appRuntimeInfo, LogEventLevel.Verbose);
        BogusExtensions.Setup();
        await MockDatabase.InitializeDatabaseTemplateAsync(pathProvider, appRuntimeInfo);
    }

    [After(TestSession)]
    public static async Task CleanupTestSession()
    {
        await MockDatabase.DisposeDatabaseTemplateAsync();
        LogFactory.CloseAndFlush();
    }
}
