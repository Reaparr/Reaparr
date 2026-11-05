using System.IO.Abstractions;
using CliWrap;
using CliWrap.EventStream;
using CliWrap.Exceptions;
using Reaparr.Logging;

namespace Reaparr.Build;

internal interface IDesktopCommandRunner
{
    void LogCommand(string fileName, IReadOnlyList<string> arguments);

    Task RunCommandAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null);

    Task<int> ExecuteCommandAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null);

    Task RequireCommandAsync(string command);

    Task<bool> CommandExistsAsync(string command);
}

internal sealed class DesktopCommandRunner : IDesktopCommandRunner
{
    private readonly BuildPaths _paths;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _log;

    public DesktopCommandRunner(BuildPaths paths, IFileSystem fileSystem, ILogger logger)
    {
        _paths = paths;
        _fileSystem = fileSystem;
        _log = logger.ForContext<DesktopCommandRunner>();
    }

    public async Task RunCommandAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        LogCommand(fileName, arguments);

        var resolvedWorkingDirectory = workingDirectory ?? _paths.RootDirectory;
        _log.Here().Information("Executing command in {WorkingDirectory}", resolvedWorkingDirectory);

        try
        {
            var command = Cli.Wrap(fileName).WithArguments(arguments).WithWorkingDirectory(resolvedWorkingDirectory);

            await foreach (var commandEvent in command.ListenAsync())
            {
                switch (commandEvent)
                {
                    case StandardOutputCommandEvent stdOut when !string.IsNullOrWhiteSpace(stdOut.Text):
                        _log.Here().Debug("{Output}", stdOut.Text);
                        break;
                    case StandardErrorCommandEvent stdErr when !string.IsNullOrWhiteSpace(stdErr.Text):
                        _log.Here().Warning("{Output}", stdErr.Text);
                        break;
                }
            }
        }
        catch (CommandExecutionException ex)
        {
            throw new InvalidOperationException(
                $"Command '{fileName}' failed with exit code {ex.ExitCode} in working directory '{resolvedWorkingDirectory}'. Full command: {FormatCommand(fileName, arguments)}",
                ex
            );
        }
    }

    public async Task<int> ExecuteCommandAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null
    )
    {
        LogCommand(fileName, arguments);

        var resolvedWorkingDirectory = workingDirectory ?? _paths.RootDirectory;
        _log.Here().Information("Executing command in {WorkingDirectory}", resolvedWorkingDirectory);

        var result = await Cli.Wrap(fileName)
            .WithArguments(arguments)
            .WithWorkingDirectory(resolvedWorkingDirectory)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        _log.Here()
            .Information(
                "Command exited with code {ExitCode}: {Command}",
                result.ExitCode,
                FormatCommand(fileName, arguments)
            );

        return result.ExitCode;
    }

    public async Task RequireCommandAsync(string command)
    {
        if (!await CommandExistsAsync(command))
        {
            throw new FileNotFoundException(
                $"Required command '{command}' was not found on PATH. Install it or adjust your environment before running desktop builds.",
                command
            );
        }
    }

    public Task<bool> CommandExistsAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return Task.FromResult(false);
        }

        if (
            _fileSystem.Path.IsPathRooted(command)
            || command.Contains(_fileSystem.Path.DirectorySeparatorChar)
            || command.Contains(_fileSystem.Path.AltDirectorySeparatorChar)
        )
        {
            return Task.FromResult(_fileSystem.File.Exists(command));
        }

        var pathValue = System.Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return Task.FromResult(false);
        }

        var candidates = new List<string> { command };

        if (OperatingSystem.IsWindows())
        {
            var pathExtValue = System.Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM";
            var pathExts = pathExtValue
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!_fileSystem.Path.HasExtension(command))
            {
                foreach (var ext in pathExts)
                {
                    candidates.Add(command + ext);
                }
            }
        }

        foreach (
            var directory in pathValue.Split(
                _fileSystem.Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            foreach (var candidate in candidates)
            {
                var fullPath = _fileSystem.Path.Combine(directory, candidate);
                if (_fileSystem.File.Exists(fullPath))
                {
                    return Task.FromResult(true);
                }
            }
        }

        return Task.FromResult(false);
    }

    public void LogCommand(string fileName, IReadOnlyList<string> arguments) =>
        _log.Here().Information("> {Command}", FormatCommand(fileName, arguments));

    private static string FormatCommand(string fileName, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { fileName }.Concat(arguments.Select(FormatArgument)));

    private static string FormatArgument(string value)
    {
        const string telemetryTokenPrefix = "-p:ReaparrTelemetryProjectToken=";
        if (value.StartsWith(telemetryTokenPrefix, StringComparison.OrdinalIgnoreCase))
            return $"{telemetryTokenPrefix}[REDACTED]";

        return QuoteIfNeeded(value);
    }

    private static string QuoteIfNeeded(string value) =>
        value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
}
