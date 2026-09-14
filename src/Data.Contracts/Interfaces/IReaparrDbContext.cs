using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Reaparr.Data.Contracts;

public interface IReaparrDbContext : IDisposable
{
    #region Properties

    #region Tables

    DbSet<PlexAccount> PlexAccounts { get; }

    DbSet<DownloadTaskMovieFileLog> DownloadTaskMovieFileLogs { get; }

    DbSet<DownloadTaskTvShowEpisodeFileLog> DownloadTaskTvShowEpisodeFileLogs { get; }

    DbSet<FolderPath> FolderPaths { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<PlexLibrary> PlexLibraries { get; }

    DbSet<PlexLibraryAccessHistoryEvent> PlexLibraryAccessHistoryEvents { get; }

    #region PlexMedia

    DbSet<PlexActor> PlexActors { get; }

    DbSet<PlexGenre> PlexGenres { get; }

    DbSet<PlexCountry> PlexCountries { get; }

    #endregion

    #region PlexMovie

    DbSet<PlexMovie> PlexMovies { get; }

    DbSet<PlexMovieMediaData> PlexMovieData { get; }

    DbSet<MediaOverviewMovieSnapshot> MediaOverviewMovieSnapshots { get; }

    #endregion

    #region PlexTvShow

    DbSet<PlexTvShow> PlexTvShows { get; }

    DbSet<PlexTvShowMediaQuality> PlexTvShowMediaQualities { get; }

    DbSet<PlexTvShowSeason> PlexTvShowSeason { get; }

    DbSet<PlexTvShowSeasonMediaQuality> PlexTvShowSeasonMediaQualities { get; }

    DbSet<PlexTvShowEpisode> PlexTvShowEpisodes { get; }

    DbSet<PlexTvShowEpisodeMediaData> PlexTvShowEpisodeData { get; }
    DbSet<MediaOverviewTvShowSnapshot> MediaOverviewTvShowSnapshots { get; }

    #endregion

    #region PlexServers

    DbSet<PlexServer> PlexServers { get; }

    DbSet<PlexServerConnection> PlexServerConnections { get; }

    DbSet<PlexServerStatus> PlexServerStatuses { get; }

    DbSet<LibrarySyncJobQueue> LibrarySyncJobQueues { get; }

    #endregion

    #endregion

    DbSet<SonarrIntegration> SonarrIntegrations { get; }

    DbSet<RadarrIntegration> RadarrIntegrations { get; }

    #region DownloadTasks

    DbSet<DownloadTaskMovie> DownloadTaskMovie { get; }

    DbSet<DownloadTaskMovieFile> DownloadTaskMovieFile { get; }

    DbSet<DownloadTaskTvShow> DownloadTaskTvShow { get; }

    DbSet<DownloadTaskTvShowSeason> DownloadTaskTvShowSeason { get; }

    DbSet<DownloadTaskTvShowEpisode> DownloadTaskTvShowEpisode { get; }

    DbSet<DownloadTaskTvShowEpisodeFile> DownloadTaskTvShowEpisodeFile { get; }

    #endregion

    #region JoinTables

    DbSet<PlexAccountServer> PlexAccountServers { get; }

    DbSet<PlexAccountLibrary> PlexAccountLibraries { get; }

    DbSet<PlexLibraryActors> PlexLibraryActors { get; }

    DbSet<PlexLibraryCountries> PlexLibraryCountries { get; }

    DbSet<PlexLibraryGenres> PlexLibraryGenres { get; }

    DbSet<PlexMovieActors> PlexMovieActors { get; }

    DbSet<PlexMovieCountries> PlexMovieCountries { get; }

    DbSet<PlexMovieGenres> PlexMovieGenres { get; }

    DbSet<PlexTvShowActors> PlexTvShowActors { get; }

    DbSet<PlexTvShowGenres> PlexTvShowGenres { get; }

    DbSet<PlexTvShowCountries> PlexTvShowCountries { get; }

    #endregion

    #region Comparison

    DbSet<PlexComparisonState> PlexComparisonScopes { get; }

    DbSet<PlexMovieComparison> PlexMovieComparisons { get; }

    DbSet<PlexTvShowComparison> PlexTvShowComparisons { get; }

    DbSet<PlexSeasonComparison> PlexSeasonComparisons { get; }

    DbSet<PlexEpisodeComparison> PlexEpisodeComparisons { get; }

    #endregion

    string DatabaseName { get; }

    #endregion Properties

    /// <summary>
    /// Inserts the supplied entities using the configured bulk-operation behavior.
    /// </summary>
    /// <typeparam name="T">The entity type to insert.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="bulkConfig">The optional bulk-operation configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous bulk-insert operation.</returns>
    Task BulkInsertAsync<T>(
        IList<T> entities,
        BulkConfig? bulkConfig = null,
        CancellationToken cancellationToken = default
    )
        where T : class;

    /// <summary>
    /// Updates the supplied entities using the configured bulk-operation behavior.
    /// </summary>
    /// <typeparam name="T">The entity type to update.</typeparam>
    /// <param name="entities">The entities to update.</param>
    /// <param name="bulkConfig">The optional bulk-operation configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous bulk-update operation.</returns>
    Task BulkUpdateAsync<T>(
        IList<T> entities,
        BulkConfig? bulkConfig = null,
        CancellationToken cancellationToken = default
    )
        where T : class;

    /// <summary>
    /// Executes an operation in a short immediate write transaction and returns its result.
    /// </summary>
    /// <typeparam name="T">The type returned by the transactional operation.</typeparam>
    /// <param name="operation">The operation to execute using this database context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A result containing the operation value when the transaction succeeds, or failure information when it fails.
    /// </returns>
    Task<Result<T>> ExecuteTransactionAsync<T>(
        Func<IReaparrDbContext, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes an operation in a short immediate write transaction.
    /// </summary>
    /// <param name="operation">The operation to execute using this database context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result indicating whether the transaction succeeded or failed.</returns>
    Task<Result> ExecuteTransactionAsync(
        Func<IReaparrDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the change-tracking entry for the supplied entity.
    /// </summary>
    /// <param name="entity">The entity whose tracking information should be retrieved.</param>
    /// <returns>The change-tracking entry for the entity.</returns>
    EntityEntry Entry(object entity);

    /// <summary>
    /// Saves all tracked changes to the database asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all tracked entities from the change tracker.
    /// </summary>
    /// <returns>This method does not return a value.</returns>
    void ClearChangeTracker();

    /// <summary>
    /// Executes interpolated SQL against the database and returns the number of affected rows.
    /// </summary>
    /// <param name="sql">The interpolated SQL command with parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows affected.</returns>
    Task<int> ExecuteSqlInterpolatedAsync(FormattableString sql, CancellationToken cancellationToken = default);
}
