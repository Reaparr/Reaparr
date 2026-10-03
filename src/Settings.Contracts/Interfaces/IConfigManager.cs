namespace Reaparr.Settings.Contracts;

public interface IConfigManager : ISetup
{
    /// <summary>
    /// Writes all settings values in the <see cref="IUserSettings"/> to the json settings file.
    /// </summary>
    /// <returns>Is successful.</returns>
    Result SaveConfig();

    Result ResetConfig();

    Result LoadConfig();

    bool ConfigFileExists();
}
