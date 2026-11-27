using System.Globalization;
using Reaparr.IntegrationTests;

namespace Reaparr.BaseTests;

[NotInParallel("IntegrationTests")]
public abstract class BaseIntegrationTests
{
    protected BaseIntegrationTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private readonly ILogger _log = LogFactory.Create<BaseIntegrationTests>();

    protected static CancellationToken CancellationToken =>
        TestContext.Current?.Execution.CancellationToken ?? CancellationToken.None;

    protected static async Task WaitForDatabaseConditionAsync(
        Func<bool> condition,
        int maxRetries = 10,
        int delayMs = 500
    )
    {
        for (var i = 0; i < maxRetries; i++)
        {
            if (condition())
                return;

            await Task.Delay(delayMs);
        }

        throw new TimeoutException(
            $"Database condition was not met after {maxRetries} retries (total wait: {maxRetries * delayMs}ms)"
        );
    }

    protected static Task WaitForDatabaseConditionAsync(
        Func<Task<bool>> condition,
        int maxRetries = 10,
        int delayMs = 500
    ) => WaitForDatabaseConditionAsync(_ => condition(), CancellationToken, maxRetries, delayMs);

    protected static async Task WaitForDatabaseConditionAsync(
        Func<CancellationToken, Task<bool>> condition,
        CancellationToken cancellationToken,
        int maxRetries = 10,
        int delayMs = 500
    )
    {
        for (var i = 0; i < maxRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await condition(cancellationToken))
                return;

            await Task.Delay(delayMs, cancellationToken);
        }

        throw new TimeoutException(
            $"Database condition was not met after {maxRetries} retries (total wait: {maxRetries * delayMs}ms)"
        );
    }

    protected static Task WaitForDownloadStatusAsync(
        BaseContainer container,
        DownloadTaskKey key,
        DownloadStatus targetStatus,
        CancellationToken cancellationToken,
        int maxRetries = 40,
        int delayMs = 500
    ) =>
        WaitForDatabaseConditionAsync(
            async _ =>
            {
                using var dbContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
                return await dbContext.GetDownloadStatusAsync(key) == targetStatus;
            },
            cancellationToken,
            maxRetries,
            delayMs
        );

    protected Task<BaseContainer> CreateContainer(int seed, Action<UnitTestDataConfig>? options = null) =>
        CreateContainer(new Seed(seed), options);

    protected Task<BaseContainer> CreateContainer(Seed seed, Action<UnitTestDataConfig>? options = null) =>
        BaseContainer.Create(_log, seed, options);
}
