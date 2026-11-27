using System.Globalization;

namespace Reaparr.BaseTests;

public partial class BaseUnitTest
{
    protected readonly ILogger Log;

    private Mock<HttpMessageHandler>? _httpHandlerMock;

    // Use loose behavior so callers do not need to set up Dispose().
    protected Mock<HttpMessageHandler> HttpHandlerMock => _httpHandlerMock ??= new(MockBehavior.Loose);

    protected static CancellationToken CancellationToken =>
        TestContext.Current?.Execution.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// This constructor is run before every test.
    /// </summary>
    protected BaseUnitTest()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        Log = LogFactory.Create<BaseUnitTest>();
    }

    public virtual void Dispose()
    {
        try
        {
            DisposeMocks();
        }
        finally
        {
            try
            {
                if (IsDatabaseSetup)
                    _setupReaparrDbContext?.EnsureDeleted();
            }
            finally
            {
                _setupReaparrDbContext?.Dispose();
                _setupAuthDbContext?.Dispose();
                _setupReaparrDbContext = null;
                _setupAuthDbContext = null;
            }
        }
    }
}

public class BaseUnitTest<TUnitTestClass> : BaseUnitTest
    where TUnitTestClass : class
{
    protected TUnitTestClass Sut => Mock.Create<TUnitTestClass>();
}
