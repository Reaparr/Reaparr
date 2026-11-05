using Autofac;
using Reaparr.Environment;
using Serilog.Sinks.TestCorrelator;

namespace Reaparr.Logging.UnitTests;

public class LogMaskingUnitTests : BaseUnitTest<LogMaskingUnitTests>
{
    [Test]
    public void ShouldHaveMaskedData_WhenLogPropertyIsConfiguredToBeMasked()
    {
        // Arrange
        try
        {
            SetAppRuntimeInfo(x => x.IsUnmasked = false);

            var runtimeInfo = Mock.Container.Resolve<IAppRuntimeInfo>();
            var pathProvider = Mock.Container.Resolve<IPathProvider>();
            var appBuildInfo = Mock.Container.Resolve<IAppBuildInfo>();
            var testLogConfig = new TestLogConfig(runtimeInfo, pathProvider, appBuildInfo);

            runtimeInfo.IsUnmasked.ShouldBeFalse();

            LogFactory.SetupLogging(testLogConfig, runtimeInfo);
            var log = LogFactory.Create<LogMaskingUnitTests>();
            using (var context = TestCorrelator.CreateContext())
            {
                // Act
                log.Debug("Test e-mail: {Email}", "john.doe@hotmail.com");
                log.Debug("Test ip: {Ip}", "http://182.18.45.10:32400/");
                log.Debug(
                    "Test ip url: {Url}",
                    "http://182.18.45.10:32400/library/sections?X-Plex-Token=PzvkRw39mjZyz4SH8Qcj"
                );
                log.Debug("Test domain name url: {Url}", "https://reykjavik.thetarsus.com/identity");
                log.Debug("Test PlexLibraryTitle property: {PlexLibraryName}", "FORBIDDEN");
                log.Debug("Test PlexAccountDisplayName property: {PlexAccountDisplayName}", "FORBIDDEN");
                log.Debug("Test PlexLibraryName property: {PlexLibraryName}", "FORBIDDEN");
                log.Debug("Test PlexServerName property: {PlexServerName}", "FORBIDDEN");
                log.Debug("Test UserName property: {UserName}", "FORBIDDEN");
                log.Debug("Test PublicAddress property: {PublicAddress}", "FORBIDDEN");
                log.Debug("Test PlexServerConnectionUrl property: {PlexServerConnectionUrl}", "FORBIDDEN");
                log.Debug("Test PlexServerConnection property: {PlexServerConnection}", "FORBIDDEN");
                log.Debug("Test PlexServerStatus property: {PlexServerStatus}", "FORBIDDEN");
                log.Debug("Test DownloadUrl property: {DownloadUrl}", "FORBIDDEN");
                log.Debug("Test AuthToken property: {AuthToken}", "FORBIDDEN");
                log.Debug("Test MachineIdentifier property: {MachineIdentifier}", "FORBIDDEN");
                log.Debug(
                    "Test destructured request: {@Request}",
                    new
                    {
                        UserName = "ReaparrRocksDEV",
                        Password = "TA%K3z05N@88p3nr02AcI$00s",
                        RememberMe = false,
                    }
                );

                // Assert
                var logEvents = TestCorrelator.GetLogEventsFromContextId(context.Id).ToList();
                logEvents.ShouldNotBeEmpty();

                foreach (var logEvent in logEvents)
                {
                    var msg = logEvent.RenderMessage();
                    msg.ShouldContain("***MASKED***");
                }
            }
        }
        finally
        {
            // Always restore the previous state and clean up logger
            LogFactory.CloseAndFlush();
        }
    }
}
