using System.IO.Abstractions;
using Reaparr.Environment;

namespace Reaparr.Settings;

public class ConfigManager : IConfigManager
{
    #region Fields

    private readonly ILogger _log;
    private readonly IPathProvider _pathProvider;

    private readonly IUserSettings _userSettings;
    private readonly IFile _file;
    private readonly IPath _path;
    private readonly IDirectory _directory;

    #endregion

    #region Constructor

    public ConfigManager(
        ILogger log,
        IPathProvider pathProvider,
        IUserSettings userSettings,
        IFile file,
        IPath path,
        IDirectory directory
    )
    {
        _log = log.ForContext<ConfigManager>();
        _pathProvider = pathProvider;
        _userSettings = userSettings;
        _file = file;
        _path = path;
        _directory = directory;
    }

    #endregion

    #region Public Methods

    public Result Setup()
    {
        _userSettings.SettingsUpdated.Subscribe(_ => SaveConfig());

        _log.Here()
            .Information(
                "Checking if {ConfigFileName} exists at {ConfigDirectory}",
                _pathProvider.ConfigFileName,
                _pathProvider.ConfigDirectory
            );

        var configDirectoryExistsResult = Result.Try(() => _directory.Exists(_pathProvider.ConfigDirectory));
        if (configDirectoryExistsResult.IsFailed)
        {
            configDirectoryExistsResult.WithError(
                "Failed to check if config directory exists, ensure it is mounted correctly and has the correct permissions"
            );
            return configDirectoryExistsResult.LogFatal();
        }

        if (configDirectoryExistsResult.Value)
        {
            _log.Here()
                .Information("Config directory exists, will use {ConfigDirectory}", _pathProvider.ConfigDirectory);
            var migrateResult = MigrateLegacyFileNames();
            if (migrateResult.IsFailed)
                return migrateResult.LogFatal();
        }
        else
        {
            _log.Here()
                .Information(
                    "Config directory does not exist, will create now at {ConfigDirectory}",
                    _pathProvider.ConfigDirectory
                );
            var createResult = Result.Try(() => _directory.CreateDirectory(_pathProvider.ConfigDirectory));
            if (createResult.IsFailed)
            {
                _log.Here()
                    .Fatal("Failed to create config directory at {ConfigDirectory}", _pathProvider.ConfigDirectory);
                return createResult.LogFatal();
            }

            _log.Here().Debug("Directory: {ConfigDirectory} created!", _pathProvider.ConfigDirectory);
        }

        if (!ConfigFileExists())
        {
            _log.Here()
                .Information(
                    "{ConfigFileName} doesn't exist, will create new one now in {ConfigDirectory}",
                    _pathProvider.ConfigFileName,
                    _pathProvider.ConfigDirectory
                );
            return SaveConfig();
        }

        var loadResult = LoadConfig();
        return loadResult.IsFailed ? loadResult : Result.Ok();
    }

    public virtual Result LoadConfig()
    {
        _log.Here().Debug("Loading user config settings now");
        var readResult = ReadFromConfigFile();
        if (readResult.IsFailed)
        {
            _log.Here()
                .Information(
                    "Resetting {ConfigFileName} because it could not be loaded correctly",
                    _pathProvider.ConfigFileName
                );
            return ResetConfig();
        }

        try
        {
            var cleanedJson = readResult.Value.Replace("\r\n", "");
            var loadedSettings = UserSettingsSerializer.Deserialize(cleanedJson);

            _userSettings.UpdateSettings(loadedSettings);

            return Result.Ok().WithSuccess("UserSettings were loaded successfully!").LogInformation();
        }
        catch (Exception e)
        {
            Result.Fail(new ExceptionalError(e)).LogError();
            _log.Here().Error("Failed to JSON parse the contents from {ConfigFileName}", _pathProvider.ConfigFileName);
            _log.Here().Error("Contents: {Contents}", readResult.Value);
            return ResetConfig();
        }
    }

    public virtual Result ResetConfig()
    {
        _userSettings.Reset();
        var saveResult = SaveConfig();
        if (saveResult.IsFailed)
            saveResult.WithError(new Error("Failed to save a new config after resetting")).LogError();

        return Result.Ok();
    }

    public virtual Result SaveConfig()
    {
        _log.Here().Debug("Saving user config settings now");

        var jsonSettings = UserSettingsSerializer.Serialize(_userSettings);

        var writeResult = WriteToConfigFile(jsonSettings);

        if (writeResult.IsFailed)
            return writeResult;

        _log.Here().Debug("UserSettings were saved successfully!");

        return Result.Ok().WithSuccess("UserSettings were saved successfully!").LogInformation();
    }

    public virtual bool ConfigFileExists() => _file.Exists(_pathProvider.ConfigFileLocation);

    #endregion

    #region Private Methods

    private Result WriteToConfigFile(string jsonSettingsString)
    {
        var writeResult = Result.Try(() => _file.WriteAllText(_pathProvider.ConfigFileLocation, jsonSettingsString));
        return writeResult.IsFailed ? writeResult.WithError("Failed to write config settings").LogError() : Result.Ok();
    }

    private Result<string> ReadFromConfigFile()
    {
        var readResult = Result.Try(() => _file.ReadAllText(_pathProvider.ConfigFileLocation));
        if (readResult.IsFailed)
        {
            _log.Here()
                .Error(
                    "Failed to read {ConfigFileName} from {ConfigDirectory}",
                    _pathProvider.ConfigFileName,
                    _pathProvider.ConfigDirectory
                );
            readResult.LogError();
        }

        return readResult;
    }

    private Result MigrateLegacyFileNames()
    {
        try
        {
            var configDirectory = _pathProvider.ConfigDirectory;

            if (!_directory.Exists(configDirectory))
                return Result.Fail("Config directory does not exist").LogFatal();

            var oldConfigPath = _path.Combine(configDirectory, "PlexRipperSettings.json");
            var newConfigPath = _pathProvider.ConfigFileLocation;

            if (_file.Exists(oldConfigPath) && !_file.Exists(newConfigPath))
            {
                _log.Here().Information("Renaming legacy config file {Old} -> {New}", oldConfigPath, newConfigPath);
                _file.Move(oldConfigPath, newConfigPath);
            }

            var oldDbPath = _path.Combine(configDirectory, "PlexRipperDB.db");
            var newDbPath = _pathProvider.DatabasePath;

            if (_file.Exists(oldDbPath) && !_file.Exists(newDbPath))
            {
                _log.Here().Information("Renaming legacy database file {Old} -> {New}", oldDbPath, newDbPath);
                _file.Move(oldDbPath, newDbPath);
            }

            var oldWalPath = oldDbPath + "-wal";
            var newWalPath = _pathProvider.DatabasePath + "-wal";
            if (_file.Exists(oldWalPath) && !_file.Exists(newWalPath))
            {
                _log.Here().Information("Renaming legacy database WAL file {Old} -> {New}", oldWalPath, newWalPath);
                _file.Move(oldWalPath, newWalPath);
            }

            var oldShmPath = oldDbPath + "-shm";
            var newShmPath = _pathProvider.DatabasePath + "-shm";
            if (_file.Exists(oldShmPath) && !_file.Exists(newShmPath))
            {
                _log.Here().Information("Renaming legacy database SHM file {Old} -> {New}", oldShmPath, newShmPath);
                _file.Move(oldShmPath, newShmPath);
            }
        }
        catch (Exception e)
        {
            _log.Here().Error("Failed legacy file rename: {Message}", e.Message);
        }

        return Result.Ok();
    }

    #endregion
}
