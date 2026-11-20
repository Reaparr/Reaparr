namespace Reaparr.Domain;

/// <summary>Flows an awaited command operation without extending its provider's lifetime.</summary>
/// <remarks>
/// FastEndpoints' static command dispatch does not accept an operation provider, so this narrowly internal AsyncLocal
/// bridges CommandExecutor's lifetime owner to CommandServiceResolver without changing handlers or swapping the global resolver.
/// Nested awaited work sees the same holder; independent execution flows can establish separate operations.
/// Task.Run also inherits this state. Work using the operation must be awaited before its owner finishes;
/// detached work needs a separate lifetime boundary, not continued use of the publishing command's provider.
/// Only CommandExecutor establishes operations. Disposing this holder restores the previous flow; the executor or
/// ASP.NET Core owns and disposes the actual scope. See https://github.com/FastEndpoints/FastEndpoints/issues/657.
/// </remarks>
internal sealed class CommandOperation : IDisposable
{
    private static readonly AsyncLocal<CommandOperation?> Current = new();
    private readonly CommandOperation? _previous;
    private IServiceProvider? _provider;

    private CommandOperation(IServiceProvider provider)
    {
        _previous = Current.Value;
        _provider = provider;
        Current.Value = this;
    }

    internal static IServiceProvider? Provider =>
        Current.Value is { } operation
            ? operation._provider ?? throw new ObjectDisposedException(nameof(CommandOperation))
            : null;

    internal static CommandOperation Enter(IServiceProvider provider) => new(provider);

    public void Dispose()
    {
        // Clear the shared holder so inherited continuations fail instead of using a disposed scope or falling back to root.
        _provider = null;
        Current.Value = _previous;
    }
}
