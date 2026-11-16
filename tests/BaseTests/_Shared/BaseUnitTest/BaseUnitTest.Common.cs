using System.Globalization;

namespace Reaparr.BaseTests;

public partial class BaseUnitTest
{
    protected readonly ILogger Log;

    // Use loose behavior here to avoid Dispose() not mocked exception
    protected Mock<HttpMessageHandler> HttpHandlerMock = new(MockBehavior.Loose);

    protected static CancellationToken CancellationToken =>
        TestContext.Current?.Execution.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// This constructor is run before every test.
    /// </summary>
    protected BaseUnitTest()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        Log = LogFactory.Create<BaseUnitTest>();
        Mock = AutoMock.GetStrict(SetDefaultBuilder);
    }

    public virtual void Dispose()
    {
        if (IsDatabaseSetup)
        {
            _setupReaparrDbContext?.EnsureDeleted();
            _setupReaparrDbContext?.Dispose();
            _setupAuthDbContext?.Dispose();
        }
    }
}

public class BaseUnitTest<TUnitTestClass> : BaseUnitTest
    where TUnitTestClass : class
{
    protected TUnitTestClass Sut => Mock.Create<TUnitTestClass>();

    public override void Dispose()
    {
        base.Dispose();
        Mock.Dispose();
    }
}
