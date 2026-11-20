namespace Reaparr.Data;

public class ReaparrDbContextFactory : IReaparrDbContextFactory
{
    private readonly ILogger _log;
    private readonly IPathProvider _pathProvider;
    private readonly IAppRuntimeInfo _appRuntimeInfo;

    public ReaparrDbContextFactory(ILogger log, IPathProvider pathProvider, IAppRuntimeInfo appRuntimeInfo)
    {
        _log = log;
        _pathProvider = pathProvider;
        _appRuntimeInfo = appRuntimeInfo;
    }

    public IReaparrDbContext Create() => new ReaparrDbContext(_log, _pathProvider, _appRuntimeInfo);

    // TODO remove this method, its a fake async so why?
#pragma warning disable MA0042
    // Calling CreateAsync here would recurse; this adapter wraps synchronous construction.
    public Task<IReaparrDbContext> CreateAsync() => Task.FromResult(Create());
#pragma warning restore MA0042
}
