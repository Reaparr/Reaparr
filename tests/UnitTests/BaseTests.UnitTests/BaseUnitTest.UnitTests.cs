using Autofac;
using Reaparr.Environment;
using Reaparr.Logging;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace Reaparr.BaseTests.UnitTests;

public class BaseUnitTestUnitTests
{
    [Test]
    public void ShouldDisposeBothGenerations_WhenAnAlreadyResolvedServiceSurvivesARebuild()
    {
        // Arrange
        using var fixture = new Fixture();
        var original = fixture.ResolveProbe();
        original.IsDisposed.ShouldBeFalse();

        // Act
        fixture.UseDesktopMode();
        var replacement = fixture.ResolveProbe();

        // Assert
        replacement.ShouldNotBeSameAs(original);
        original.BuildInfo.RuntimeMode.ShouldBe("desktop");
        replacement.BuildInfo.RuntimeMode.ShouldBe("desktop");
        original.IsDisposed.ShouldBeFalse();
        replacement.IsDisposed.ShouldBeFalse();

        fixture.Dispose();
        original.IsDisposed.ShouldBeTrue();
        replacement.IsDisposed.ShouldBeTrue();
    }

    [Test]
    public void ShouldKeepSessionLoggingAlive_WhenFixtureIsDisposed()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        using var fixture = new Fixture();
        var logger = fixture.ResolveLogger();
        logger.Debug("Before fixture disposal");

        // Act
        fixture.Dispose();
        LogFactory.Create().Debug("After fixture disposal");

        // Assert
        TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(logEvent => logEvent.Level == LogEventLevel.Debug)
            .Select(logEvent => logEvent.MessageTemplate.Text)
            .ShouldBe(["Before fixture disposal", "After fixture disposal"]);
    }

    private sealed class Fixture : BaseUnitTest
    {
        public Fixture()
        {
            SetupDependencies(builder => builder.RegisterType<Probe>().SingleInstance());
        }

        public Probe ResolveProbe() => Mock.Container.Resolve<Probe>();

        public Serilog.ILogger ResolveLogger() => Mock.Container.Resolve<Serilog.ILogger>();

        public void UseDesktopMode() => SetAppBuildInfo(info => info.RuntimeMode = "desktop");
    }

    private sealed class Probe : IDisposable
    {
        public Probe(IAppBuildInfo buildInfo)
        {
            BuildInfo = buildInfo;
        }

        public IAppBuildInfo BuildInfo { get; }

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
