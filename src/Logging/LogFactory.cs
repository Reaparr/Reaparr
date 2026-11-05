using System.Runtime.CompilerServices;

namespace Reaparr.Logging;

/// <summary>
/// Configures the shared Serilog logger and provides contextual loggers for application code.
/// </summary>
public static class LogFactory
{
    /// <summary>
    /// Gets the minimum log level selected when logging was last configured.
    /// </summary>
    public static LogEventLevel MinimumLogLevel { get; private set; }

    /// <summary>
    /// Routes supported EF Core diagnostic messages to Debug, Information, or Error with caller context.
    /// </summary>
    /// <param name="messageTemplate">The diagnostic message, prefixed with dbug:, info:, or fail:.</param>
    /// <param name="memberName">The calling member, supplied automatically.</param>
    /// <param name="sourceFilePath">The calling source file, supplied automatically.</param>
    /// <param name="sourceLineNumber">The calling source line, supplied automatically.</param>
    [MessageTemplateFormatMethod("messageTemplate")]
    public static void DbContextLogger(
        string messageTemplate,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        switch (messageTemplate)
        {
            case { } s when s.StartsWith("dbug:"):
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                Create().Here(sourceFilePath, memberName, sourceLineNumber).Debug(messageTemplate);
                break;
            case { } s when s.StartsWith("info:"):
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                Create().Here(sourceFilePath, memberName, sourceLineNumber).Information(messageTemplate);
                break;
            case { } s when s.StartsWith("fail:"):
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                Create().Here(sourceFilePath, memberName, sourceLineNumber).Error(messageTemplate);
                break;
        }
    }

    /// <summary>
    /// Installs the configured shared logger, records its minimum level, and writes startup diagnostics.
    /// </summary>
    /// <param name="logConfig">The configuration used to create the logger and its sinks.</param>
    /// <param name="appRuntimeInfo">Runtime information used by the startup diagnostics.</param>
    /// <param name="minimumLogLevel">The minimum level used to configure logging.</param>
    public static void SetupLogging(
        LogConfig logConfig,
        IAppRuntimeInfo appRuntimeInfo,
        LogEventLevel minimumLogLevel = LogEventLevel.Debug
    )
    {
        ArgumentNullException.ThrowIfNull(logConfig);
        MinimumLogLevel = minimumLogLevel;
        Log.Logger = logConfig.GetLogger(minimumLogLevel);
        var log = Create();

        log.Here().Information("Starting Reaparr! App run ID: {AppRunId}", appRuntimeInfo.AppRunId);

        log.Here().Information("Logging level set to {LogLevel}", MinimumLogLevel);

        if (appRuntimeInfo.IsUnmasked)
        {
            log.Here()
                .Warning(
                    "Environment variable {UnmaskedKey} has been set to true, which means that sensitive data will be shown in the logs!",
                    EnvKeys.Unmasked
                );

            log.Here().Warning("This username should be shown: {Username}", "SomeSecretUsername");
        }
    }

    /// <summary>
    /// Flushes pending log events and disposes the shared logger and its sinks.
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }

    /// <summary>
    /// Creates a logger enriched with the specified type's source context.
    /// </summary>
    /// <typeparam name="T">The type associated with the log events.</typeparam>
    /// <returns>A contextual logger backed by the shared logger.</returns>
    public static ILogger Create<T>() => Log.Logger.ForContext<T>();

    /// <summary>
    /// Creates a logger enriched with the specified type's source context.
    /// </summary>
    /// <param name="classType">The type associated with the log events.</param>
    /// <returns>A contextual logger backed by the shared logger.</returns>
    public static ILogger Create(Type classType) => Log.Logger.ForContext(classType);

    /// <summary>
    /// Gets the current shared Serilog logger without adding source context.
    /// </summary>
    /// <returns>The application's shared logger.</returns>
    public static ILogger Create() => Log.Logger;
}
