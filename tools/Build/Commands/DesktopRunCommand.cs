using Reaparr.Domain;
using Spectre.Console.Cli;

namespace Reaparr.Build;

internal sealed class DesktopRunCommand(ICommandExecutor commandExecutor) : AsyncCommand<DesktopCommandSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        DesktopCommandSettings settings,
        CancellationToken cancellationToken
    )
    {
        var result = await commandExecutor.Send(new DesktopRunBuildCommand(settings), cancellationToken);

        return result.IsFailed ? 1 : result.Value;
    }
}
