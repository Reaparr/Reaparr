using Reaparr.Logging;
using Spectre.Console.Cli;

namespace Reaparr.Build;

public static partial class Startup
{
    public static CommandApp ConfigureConsole(this CommandApp app, ILogger logger)
    {
        var log = logger.ForContext(typeof(Startup));

        app.Configure(config =>
        {
            config.SetApplicationName("reaparr-build");
            config.UseStrictParsing();
            config.PropagateExceptions();
            config.CaseSensitivity(CaseSensitivity.None);
            config.SetApplicationVersion(typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0");

            config.AddExample(
                "desktop",
                "publish",
                "--rid",
                "linux-x64",
                "--version",
                "0.0.1",
                "--informational-version",
                "0.0.1-local"
            );
            config.AddExample(
                "desktop",
                "package",
                "--rid",
                "osx-arm64",
                "--version",
                "0.0.1",
                "--informational-version",
                "0.0.1-local",
                "--dry-run"
            );
            config.AddExample(
                "desktop",
                "run",
                "--rid",
                "win-x64",
                "--version",
                "0.0.1",
                "--informational-version",
                "0.0.1-local"
            );
            config.AddExample(
                "desktop",
                "ci-package",
                "--rid",
                "linux-x64",
                "--version",
                "0.0.1",
                "--informational-version",
                "0.0.1-dev.1",
                "--frontend-public-dir",
                ".tmp/frontend-public",
                "--artifact-dir",
                "Releases"
            );

            config.AddBranch("desktop", desktop =>
            {
                desktop
                    .AddCommand<DesktopPublishCommand>("publish")
                    .WithDescription("Generate frontend assets and publish AppHost for a desktop RID.")
                    .WithExample(
                        "desktop",
                        "publish",
                        "--rid",
                        "linux-x64",
                        "--version",
                        "0.0.1",
                        "--informational-version",
                        "0.0.1-local"
                    );

                desktop
                    .AddCommand<DesktopPackageCommand>("package")
                    .WithDescription("Publish AppHost and create Velopack artifacts for a desktop RID.")
                    .WithAlias("pack")
                    .WithExample(
                        "desktop",
                        "package",
                        "--rid",
                        "osx-arm64",
                        "--version",
                        "0.0.1",
                        "--informational-version",
                        "0.0.1-local",
                        "--dry-run"
                    );

                desktop
                    .AddCommand<DesktopCiPackageCommand>("ci-package")
                    .WithDescription(
                        "Use pre-generated frontend assets and perform the RID-specific restore and packaging steps for a desktop build in CI."
                    )
                    .WithExample(
                        "desktop",
                        "ci-package",
                        "--rid",
                        "linux-x64",
                        "--version",
                        "0.0.1",
                        "--informational-version",
                        "0.0.1-dev.1",
                        "--frontend-public-dir",
                        ".tmp/frontend-public",
                        "--artifact-dir",
                        "Releases"
                    );

                desktop
                    .AddCommand<DesktopRunCommand>("run")
                    .WithDescription("Publish or package as needed, then launch the desktop app when supported.")
                    .WithExample(
                        "desktop",
                        "run",
                        "--rid",
                        "win-x64",
                        "--version",
                        "0.0.1",
                        "--informational-version",
                        "0.0.1-local"
                    );
            });

            config.SetExceptionHandler(
                (ex, _) =>
                {
                    log.Here().Error(ex, "{Message}", ex.Message);
                    return -1;
                }
            );
        });

        return app;
    }
}
