using Autofac;
using Reaparr.Data;
using Reaparr.Environment;
using Reaparr.Identity;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace Reaparr.BaseTests.UnitTests;

public class BaseUnitTestUnitTests
{
    [Test]
    public void ShouldDisposeBothGenerations_WhenAnAlreadyResolvedServiceSurvivesARebuild()
    {
        // Arrange
        Probe original;
        Probe replacement;
        using (var fixture = new Fixture())
        {
            original = fixture.ResolveProbe();
            original.IsDisposed.ShouldBeFalse();

            // Act
            fixture.UseDesktopMode();
            replacement = fixture.ResolveProbe();

            // Assert
            replacement.ShouldNotBeSameAs(original);
            original.BuildInfo.RuntimeMode.ShouldBe("desktop");
            replacement.BuildInfo.RuntimeMode.ShouldBe("desktop");
            original.IsDisposed.ShouldBeFalse();
            replacement.IsDisposed.ShouldBeFalse();
        }

        original.IsDisposed.ShouldBeTrue();
        replacement.IsDisposed.ShouldBeTrue();
    }

    [Test]
    public void ShouldKeepSessionLoggingAlive_WhenFixtureIsDisposed()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        using (var fixture = new Fixture())
        {
            var logger = fixture.ResolveLogger();
            logger.Debug("Before fixture disposal");
        }

        // Act
        LogFactory.Create().Debug("After fixture disposal");

        // Assert
        TestCorrelator
            .GetLogEventsFromCurrentContext()
            .Where(logEvent => logEvent.Level == LogEventLevel.Debug)
            .Select(logEvent => logEvent.MessageTemplate.Text)
            .ShouldBe(["Before fixture disposal", "After fixture disposal"]);
    }

    [Test]
    public async Task ShouldCloseOnlyItsOwnConnections_WhenFixtureIsDisposed()
    {
        // Arrange
        using var second = new Fixture();
        await second.SeedDatabase();
        var secondContext = second.OpenContext();
        await secondContext.Database.OpenConnectionAsync();

        System.Data.Common.DbConnection firstConnection;
        System.Data.Common.DbConnection firstAuthConnection;
        using (var first = new Fixture())
        {
            await first.SeedDatabase();
            var firstContext = first.OpenContext();
            var firstAuth = first.OpenAuthContext();
            await firstContext.Database.OpenConnectionAsync();
            await firstAuth.Database.OpenConnectionAsync();
            firstConnection = firstContext.Database.GetDbConnection();
            firstAuthConnection = firstAuth.Database.GetDbConnection();
        }

        // Assert
        firstConnection.State.ShouldBe(System.Data.ConnectionState.Closed);
        firstAuthConnection.State.ShouldBe(System.Data.ConnectionState.Closed);
        await secondContext.PlexServers.SingleAsync();
    }

    private sealed class Fixture : BaseUnitTest
    {
        public Fixture()
        {
            SetupDependencies(builder => builder.RegisterType<Probe>().SingleInstance());
        }

        public Probe ResolveProbe() => Mock.Container.Resolve<Probe>();

        public ILogger ResolveLogger() => Mock.Container.Resolve<ILogger>();

        public Task<Seed> SeedDatabase() => SetupDatabase(421);

        public ReaparrDbContext OpenContext() => (ReaparrDbContext)IDbContext;

        public AuthDbContext OpenAuthContext() => (AuthDbContext)AuthDbContext;

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
