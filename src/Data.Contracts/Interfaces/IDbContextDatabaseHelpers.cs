using System.Data.Common;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Reaparr.Data.Contracts;

public interface IDbContextDatabaseHelpers
{
    /// <summary>
    ///     Determines whether the database is available and can be connected to.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Any exceptions thrown when attempting to connect are caught and not propagated to the application.
    ///     </para>
    ///     <para>
    ///         The configured connection string is used to create the connection in the normal way, so all
    ///         configured options such as timeouts are honored.
    ///     </para>
    ///     <para>
    ///         Note that being able to connect to the database does not mean that it is
    ///         up-to-date with regard to schema creation, etc.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-connections">Database connections in EF Core</see> for more information and examples.
    ///     </para>
    /// </remarks>
    /// <returns><see langword="true" /> if the database is available; <see langword="false" /> otherwise.</returns>
    bool CanConnect();

    /// <summary>
    ///     Returns <see langword="true" /> if the database provider currently in use is the in-memory provider.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method can only be used after the <see cref="DbContext" /> has been configured because
    ///         it is only then that the provider is known. This means that this method cannot be used
    ///         in DbContext.OnConfiguring because this is where application code sets the
    ///         provider to use as part of configuring the context.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-in-memory">The EF Core in-memory database provider</see> for more information and
    ///         examples.
    ///     </para>
    /// </remarks>
    /// <returns><see langword="true" /> if the in-memory database is being used.</returns>
    bool IsInMemory();

    /// <summary>
    ///     Closes the underlying <see cref="DbConnection" />.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-connections">Connections and connection strings</see> for more information and examples.
    /// </remarks>
    void CloseConnection();

    /// <summary>
    ///     <para>
    ///         Ensures that the database for the context does not exist. If it does not exist, no action is taken. If it does
    ///         exist then the database is deleted.
    ///     </para>
    ///     <para>
    ///         Warning: The entire database is deleted, and no effort is made to remove just the database objects that are used by
    ///         the model for this context.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         It is common to use EnsureCreated immediately following EnsureDeleted when
    ///         testing or prototyping using Entity Framework. This ensures that the database is in a clean state before each
    ///         execution of the test/prototype. Note, however, that data in the database is not preserved.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-manage-schemas">Managing database schemas with EF Core</see>
    ///         and <see href="https://aka.ms/efcore-docs-ensure-created">Database creation APIs</see> for more information and examples.
    ///     </para>
    /// </remarks>
    /// <returns><see langword="true" /> if the database is deleted, <see langword="false" /> if it did not exist.</returns>
    Result<bool> EnsureDeleted();

    /// <summary>
    ///     Applies any pending migrations for the context to the database. Will create the database
    ///     if it does not already exist.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Note that this API is mutually exclusive with <see cref="DatabaseFacade.EnsureCreated" />. EnsureCreated does not use migrations
    ///         to create the database and therefore the database that is created cannot be later updated using migrations.
    ///     </para>
    ///     <para>
    ///         See <see href="https://aka.ms/efcore-docs-migrations">Database migrations</see> for more information and examples.
    ///     </para>
    /// </remarks>
    Result Migrate();

    /// <summary>
    ///     Gets all migrations that are defined in the assembly but haven't been applied to the target database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-migrations">Database migrations</see> for more information and examples.
    /// </remarks>
    /// <returns>The list of migrations.</returns>
    IEnumerable<string> GetPendingMigrations();

    /// <summary>
    /// Optimizes the database by performing maintenance tasks such as vacuuming, reindexing, and analyzing the database.
    /// </summary>
    Result<int> Optimize(CancellationToken cancellationToken = default);
}
