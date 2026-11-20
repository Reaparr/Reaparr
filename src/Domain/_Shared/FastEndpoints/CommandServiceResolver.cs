using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Domain;

/// <summary>Preserves FastEndpoints activation while honoring the current command's lifetime owner.</summary>
/// <remarks>
/// IServiceResolver is FastEndpoints' public customization point. This adapter is registered once, not replaced per command.
/// Per-dispatch activation uses the explicit provider, current command operation, HTTP request, then application root.
/// Root fallback remains for framework infrastructure; background commands must enter through ICommandExecutor to get ownership.
/// Factory delegates cache construction logic, not operation providers. Framework singletons always use the root provider
/// so the first command/request cannot become their lifetime owner: https://github.com/FastEndpoints/FastEndpoints/pull/1179.
/// The cached framework command executor is not the command handler; handlers and middleware are activated per dispatch:
/// https://github.com/FastEndpoints/FastEndpoints/issues/991.
/// FastEndpoints 9 resolves middleware discovery through this resolver even when its executor is constructed from root.
/// That lets us retain the framework pipeline without recognizing internal executor types or resolving their singletons
/// from temporary scopes: https://github.com/FastEndpoints/FastEndpoints/commit/15ffafe8219b1dc91fd6847170c81c4bc360f7ed.
/// </remarks>
public sealed class CommandServiceResolver : IServiceResolver
{
    private readonly IServiceProvider _root;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ConcurrentDictionary<Type, ObjectFactory> _factories = new();
    private readonly ConcurrentDictionary<Type, object> _singletons = new();

    public CommandServiceResolver(IServiceProvider root, IHttpContextAccessor httpContextAccessor)
    {
        _root = root;
        _httpContextAccessor = httpContextAccessor;
    }

    private IServiceProvider Provider =>
        CommandOperation.Provider ?? _httpContextAccessor.HttpContext?.RequestServices ?? _root;

    public object CreateInstance(Type type, IServiceProvider? serviceProvider = null) =>
        _factories.GetOrAdd(type, static t => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes))(
            serviceProvider ?? Provider,
            null
        );

    public object CreateSingleton(Type type) =>
        _singletons.GetOrAdd(type, t => ActivatorUtilities.GetServiceOrCreateInstance(_root, t));

    public IServiceScope CreateScope() => Provider.CreateScope();

    public TService? TryResolve<TService>()
        where TService : class => Provider.GetService<TService>();

    public object? TryResolve(Type typeOfService) => Provider.GetService(typeOfService);

    public TService Resolve<TService>()
        where TService : class => Provider.GetRequiredService<TService>();

    public object Resolve(Type typeOfService) => Provider.GetRequiredService(typeOfService);

    public TService? TryResolve<TService>(string keyName)
        where TService : class => Provider.GetKeyedService<TService>(keyName);

    public object? TryResolve(Type typeOfService, string keyName) =>
        ((IKeyedServiceProvider)Provider).GetKeyedService(typeOfService, keyName);

    public TService Resolve<TService>(string keyName)
        where TService : class => Provider.GetRequiredKeyedService<TService>(keyName);

    public object Resolve(Type typeOfService, string keyName) =>
        Provider.GetRequiredKeyedService(typeOfService, keyName);
}
