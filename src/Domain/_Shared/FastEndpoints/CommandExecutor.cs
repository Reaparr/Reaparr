using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Domain;

/// <summary>
/// Owns the dependency lifetime of awaited command execution without replacing FastEndpoints' command pipeline.
/// </summary>
/// <remarks>
/// Without an HTTP request, FastEndpoints normally resolves handler dependencies from the application provider. Autofac then retains disposable transient dependencies, including database contexts, until application shutdown rather than command completion (Reaparr issue: https://github.com/Reaparr/Reaparr/issues/716).
/// Nested commands reuse the current operation; HTTP commands borrow the request provider; top-level background commands own a new async scope. This controls disposal only: it does not share transient contexts or add a transaction. Creating a scope around ExecuteAsync alone is insufficient because FastEndpoints dispatch uses its global resolver. CommandOperation and CommandServiceResolver make that resolver use the selected provider while preserving discovery, middleware, validation, and the existing exception-to-result boundary in this executor. FastEndpoints' background-scoping limitation: https://github.com/FastEndpoints/FastEndpoints/issues/657.
/// </remarks>
public class CommandExecutor : ICommandExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CommandExecutor(IServiceScopeFactory scopeFactory, IHttpContextAccessor httpContextAccessor)
    {
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken ct = default)
        where TResult : ResultBase, new()
    {
        var executionResult = await Result.Try(async Task<TResult> () =>
        {
            if (CommandOperation.Provider is not null)
                return await command.ExecuteAsync(ct);

            var requestProvider = _httpContextAccessor.HttpContext?.RequestServices;
            if (requestProvider is not null)
            {
                using var requestOperation = CommandOperation.Enter(requestProvider);
                return await command.ExecuteAsync(ct);
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            using var operation = CommandOperation.Enter(scope.ServiceProvider);
            return await command.ExecuteAsync(ct);
        });
        if (executionResult.IsSuccess)
            return executionResult.Value;

        var failedResult = new TResult();
        failedResult.Reasons.AddRange(executionResult.Errors);
        return failedResult;
    }
}
