using Reaparr.Domain;
using Spectre.Console.Cli;

namespace Reaparr.Build;

internal sealed class DesktopCiPackageCommand(ICommandExecutor commandExecutor) : AsyncCommand<DesktopCommandSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        DesktopCommandSettings settings,
        CancellationToken cancellationToken
    )
    {
        var ciSettings = CreateCiSettings(settings);
        var result = await commandExecutor.Send(new DesktopPackageBuildCommand(ciSettings), cancellationToken);

        return result.IsFailed ? 1 : result.Value;
    }

    internal static DesktopCommandSettings CreateCiSettings(DesktopCommandSettings settings) =>
        new()
        {
            RuntimeIdentifier = settings.RuntimeIdentifier,
            Version = settings.Version,
            InformationalVersion = settings.InformationalVersion,
            Channel = settings.Channel,
            PostHogHost = settings.PostHogHost,
            PostHogProjectToken = settings.PostHogProjectToken,
            SkipFrontend = true,
            SkipRestore = false,
            SkipPackage = false,
            DryRun = settings.DryRun,
            FrontendPublicDirectory = settings.FrontendPublicDirectory,
            ArtifactDirectory = settings.ArtifactDirectory,
            PreserveExistingArtifacts = settings.PreserveExistingArtifacts,
            LaunchMode = settings.LaunchMode,
        };
}
