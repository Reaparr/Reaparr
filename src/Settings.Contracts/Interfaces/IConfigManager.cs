namespace Reaparr.Settings.Contracts;

public interface IConfigManager : ISetup
{
    /// <summary>
    /// Emitted after a configuration has been successfully written to disk.
    /// </summary>
    IObservable<System.Reactive.Unit> SettingsSaved { get; }

    /// <summary>
    /// Writes all settings values in the <see cref="IUserSettings"/> to the json settings file.
    /// </summary>
    /// <returns>Is successful.</returns>
    Result SaveConfig();

    Result ResetConfig();

    Result LoadConfig();

    bool ConfigFileExists();
}
