using System.Globalization;
using System.IO.Abstractions;

namespace Reaparr.Data;

public class ReaparrDbContextManager : IReaparrDbContextManager
{
    private readonly ILogger _log;

    private readonly IAppRuntimeInfo _appRuntimeInfo;
    private readonly IReaparrDbContextDatabase _reaparrDbContextDatabase;
    private readonly IAuthDbContextDatabase _authDbContextDatabase;
    private readonly IGeneralSettings _generalSettings;

    private readonly IPathProvider _pathProvider;

    private readonly IDirectory _directory;
    private readonly IFile _file;
    private string DatabasePath => _pathProvider.DatabasePath;

    public ReaparrDbContextManager(
        ILogger log,
        IAppRuntimeInfo appRuntimeInfo,
        IReaparrDbContextDatabase reaparrDbContextDatabase,
        IAuthDbContextDatabase authDbContextDatabase,
        IGeneralSettings generalSettings,
        IPathProvider pathProvider,
        IDirectory directory,
        IFile file
    )
    {
        _log = log.ForContext<ReaparrDbContextManager>();
        _appRuntimeInfo = appRuntimeInfo;
        _reaparrDbContextDatabase = reaparrDbContextDatabase;
        _authDbContextDatabase = authDbContextDatabase;
        _generalSettings = generalSettings;
        _pathProvider = pathProvider;
        _directory = directory;
        _file = file;
    }

    public async Task<Result> SetupAsync(CancellationToken cancellationToken = default)
    {
        if (_appRuntimeInfo.IsIntegrationTestMode)
        {
            _log.Here().Information("Integration test mode detected, skipping database setup");
            return Result.Ok();
        }

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(SetupAsync));

        if (_file.Exists(DatabasePath))
        {
            // Check if the database can be connected to.
            if (_reaparrDbContextDatabase.CanConnect())
            {
                _log.Here().Information("Database was successfully connected!");
                _log.Here().Information("Database connected at: {DatabasePath}", DatabasePath);

                var migrateResult = await MigrateDatabase(cancellationToken);
                if (migrateResult.IsFailed)
                    return migrateResult;

                return Result.Ok();
            }

            _log.Here()
                .Error(
                    "Database exists at {DatabasePath} but could not be connected to, resetting database now",
                    DatabasePath
                );
            return await ResetDatabase();
        }

        _log.Here().Warning("Database does not exist, creating a new one now");

        var createResult = await CreateDatabase(cancellationToken);
        if (createResult.IsFailed)
            return createResult;

        return Result.Ok();
    }

    public async Task<Result> ResetDatabase()
    {
        try
        {
            _log.Here().Information("Resetting Reaparr database now");
            _reaparrDbContextDatabase.CloseConnection();

            var backUpResult = BackUpDatabase();
            if (backUpResult.IsFailed)
            {
                _log.Here().Error("Failed to back-up database");
                return backUpResult.LogError();
            }

            var deletedResult = _reaparrDbContextDatabase.EnsureDeleted();
            if (deletedResult.IsFailed)
            {
                _log.Here().Error("Database could not be deleted at {DatabasePath}", DatabasePath);
                return deletedResult.LogError();
            }

            if (deletedResult.Value)
                _log.Here().Warning("Database was successfully deleted at: {DatabasePath}", DatabasePath);

            var createdResult = await CreateDatabase();
            if (createdResult.IsFailed)
            {
                _log.Here().Error("Database could not be created at {DatabasePath}", DatabasePath);
                return createdResult.LogError();
            }

            _generalSettings.FirstTimeSetup = true;
            _log.Here().Information("First time setup has been set to true because the database has been reset");

            return Result.Ok();
        }
        catch (Exception e)
        {
            _log.Here().Fatal("Failed to reset database!");
            _log.Here().Fatal("TO FIX THIS: DELETE DATABASE MANUALLY FROM THE CONFIG DIRECTORY");
            return Result.Fail(new ExceptionalError(e)).LogFatal();
        }
    }

    private async Task<Result> CreateDatabase(CancellationToken cancellationToken = default)
    {
        try
        {
            // Create the database while applying any pending migrations.
            var reaparrMigrateResult = _reaparrDbContextDatabase.Migrate();
            var authMigrateResult = _authDbContextDatabase.Migrate();

            if (reaparrMigrateResult.IsFailed || authMigrateResult.IsFailed)
            {
                _log.Here().Error("Failed to create the database because one or more migrations failed");
                reaparrMigrateResult.LogError();
                authMigrateResult.LogError();

                return Result.Merge(reaparrMigrateResult, authMigrateResult).LogError();
            }

            var optimize = await _reaparrDbContextDatabase.Optimize(cancellationToken);
            optimize.LogIfFailed();

            _log.Here().Information("The new database was successfully created at: {DatabasePath}", DatabasePath);
            return Result.Ok();
        }
        catch (Exception e)
        {
            _log.Here().Error("Failed to create the database");
            _log.Here().ErrorResult(e);

            return Result.Fail(new ExceptionalError(e)).LogError();
        }
    }

    private async Task<Result> MigrateDatabase(CancellationToken cancellationToken)
    {
        try
        {
            // Don't migrate when running in memory, this causes an error:
            // "Relational-specific methods can only be used when the context is using a relational database provider."
            var pendingMigrations = _reaparrDbContextDatabase.GetPendingMigrations();
            if (!_reaparrDbContextDatabase.IsInMemory() && pendingMigrations.Any())
            {
                _log.Here().Information("Attempting to migrate database, this might take a while");
                var migrateResult = _reaparrDbContextDatabase.Migrate();
                if (migrateResult.IsFailed)
                {
                    _log.Here().Error("Failed to migrate the database");
                    migrateResult.LogError();
                    var resetResult = await ResetDatabase();
                    if (resetResult.IsFailed)
                        return resetResult;
                }
                else
                {
                    var optimize = await _reaparrDbContextDatabase.Optimize(cancellationToken);
                    optimize.LogIfFailed();
                    _log.Here().Information("Database migration successful!");
                }
            }

            pendingMigrations = _authDbContextDatabase.GetPendingMigrations();
            if (!_authDbContextDatabase.IsInMemory() && pendingMigrations.Any())
            {
                _log.Here().Information("Attempting to migrate Authentication tables database");
                var migrateResult = _authDbContextDatabase.Migrate();
                if (migrateResult.IsFailed)
                {
                    _log.Here().Error("Failed to migrate Authentication tables database");
                    migrateResult.LogError();
                    var resetResult = await ResetDatabase();
                    if (resetResult.IsFailed)
                        return resetResult;
                }
                else
                {
                    _log.Here().Information("Authentication tables migration successful!");
                }
            }

            return Result.Ok();
        }
        catch (Exception e)
        {
            _log.Here().Error("Failed to migrate the database or the database is corrupted");
            _log.Here().ErrorResult(e);

            return await ResetDatabase();
        }
    }

    private Result BackUpDatabase()
    {
        _log.Here().Information("Attempting to back-up the Reaparr database");
        if (!_file.Exists(_pathProvider.DatabasePath))
        {
            _log.Here().Information("Database does not exist, cannot continue to back-up");
            return Result.Ok();
        }

        var dateString = DateTime.UtcNow.ToString("yy-MM-dd_hh-mm", CultureInfo.InvariantCulture);
        var dbBackUpPath = Path.Combine(_pathProvider.DatabaseBackupDirectory, dateString);

        var createDirectoryResult = Result.Try(() => _directory.CreateDirectory(dbBackUpPath));
        if (createDirectoryResult.IsFailed)
        {
            _log.Here().Error("Failed to create back-up directory at {DbBackUpPath}", dbBackUpPath);
            return createDirectoryResult.LogError();
        }

        // Wait until the database is available.
        StreamExtensions
            .WaitForFile(_pathProvider.DatabasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            ?.Dispose();

        foreach (var databaseFilePath in _pathProvider.DatabaseFiles)
        {
            if (_file.Exists(databaseFilePath))
            {
                var combineResult = Result.Try(() => Path.Combine(dbBackUpPath, databaseFilePath.GetFileName()));
                if (combineResult.IsFailed)
                    return combineResult.LogError();

                var destinationPath = combineResult.Value;

                var copyResult = Result.Try((() => _file.Copy(databaseFilePath, destinationPath)));
                if (copyResult.IsFailed)
                {
                    _log.Here()
                        .Error(
                            "Failed to copy {DatabaseFilePath} to back-up location {DestinationPath}",
                            databaseFilePath,
                            destinationPath
                        );
                    return copyResult.LogError();
                }

                _log.Here()
                    .Information(
                        "Successfully copied \"{DatabaseFilePath}\" to back-up location\"{DestinationPath}\"",
                        databaseFilePath,
                        destinationPath
                    );

                continue;
            }

            _log.Here().Warning("Could not find: {DatabaseFilePath} to backup", databaseFilePath);
        }

        return Result.Ok();
    }
}
