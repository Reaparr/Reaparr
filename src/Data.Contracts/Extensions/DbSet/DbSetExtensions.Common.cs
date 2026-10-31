using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Reaparr.Data.Contracts;

public static partial class DbSetExtensions
{
    public static async Task<TEntity?> GetAsync<TEntity>(
        this DbSet<TEntity> dbSet,
        int id,
        CancellationToken cancellationToken = default
    )
        where TEntity : class => await dbSet.FindAsync([id], cancellationToken);

    public static async Task<TEntity?> GetAsync<TEntity>(
        this DbSet<TEntity> dbSet,
        Guid guid,
        CancellationToken cancellationToken = default
    )
        where TEntity : class => await dbSet.FindAsync([guid], cancellationToken);

    public static async Task<TEntity?> GetAsync<TEntity>(
        this IQueryable<TEntity> queryable,
        int id,
        CancellationToken cancellationToken = default
    )
        where TEntity : BaseEntity => await queryable.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public static async Task<TEntity?> GetAsync<TEntity>(
        this IQueryable<TEntity> queryable,
        Guid guid,
        CancellationToken cancellationToken = default
    )
        where TEntity : BaseEntityGuid => await queryable.FirstOrDefaultAsync(x => x.Id == guid, cancellationToken);

    /// <summary>
    /// Conditionally applies a Take clause to the provided query based on the specified limit.
    /// If the limit is greater than 0, it restricts the query results to the specified number of items.
    /// If the limit is 0 or less, it returns the query without any restriction, effectively retrieving all items.
    /// </summary>
    /// <typeparam name="T">The type of the entities in the query.</typeparam>
    /// <param name="query">The IQueryable representing the query to which the Take clause will be applied.</param>
    /// <param name="take">The number of items to take from the query. If take is 0 or less, all items will be retrieved.</param>
    /// <returns>An IQueryable with the conditional Take clause applied.</returns>
    public static IQueryable<T> ApplyTake<T>(this IQueryable<T> query, int take) => take > 0 ? query.Take(take) : query;

    /// <summary>
    /// Conditionally applies a Skip clause to the provided query based on the specified number of items to skip.
    /// If the skip value is greater than 0, it skips the specified number of items in the query results.
    /// If the skip value is 0 or less, it returns the query without skipping any items.
    /// </summary>
    /// <typeparam name="T">The type of the entities in the query.</typeparam>
    /// <param name="query">The IQueryable representing the query to which the Skip clause will be applied.</param>
    /// <param name="skip">The number of items to skip in the query results. If skip is 0 or less, no items will be skipped.</param>
    /// <returns>An IQueryable with the conditional Skip clause applied.</returns>
    public static IQueryable<T> ApplySkip<T>(this IQueryable<T> query, int skip) => skip > 0 ? query.Skip(skip) : query;

    /// <summary>
    /// Conditionally applies a Where clause to the provided query based on the specified condition.
    /// If the condition is true, the Where clause is applied using the given predicate.
    /// If the condition is false, the query is returned without modification.
    /// </summary>
    /// <typeparam name="T">The type of the entities in the query.</typeparam>
    /// <param name="query">The IQueryable representing the query to which the Where clause will be applied.</param>
    /// <param name="condition">A boolean value determining whether to apply the Where clause.</param>
    /// <param name="predicate">An expression representing the condition to be applied in the Where clause.</param>
    /// <returns>
    /// An IQueryable with the Where clause applied if the condition is true; otherwise, the original query.
    /// </returns>
    public static IQueryable<T> ApplyWhere<T>(
        this IQueryable<T> query,
        bool condition,
        Expression<Func<T, bool>> predicate
    ) => condition ? query.Where(predicate) : query;

    /// <summary>
    /// Applies an OrderBy clause to the query if the specified condition is true.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements in the query.</typeparam>
    /// <typeparam name="TKey">The type of the key used for ordering.</typeparam>
    /// <param name="query">The queryable collection to which the OrderBy clause will be applied.</param>
    /// <param name="condition">A boolean value determining whether to apply the OrderBy clause.</param>
    /// <param name="predicate">The expression specifying the key selector for sorting.</param>
    /// <returns>
    /// The modified queryable collection with the OrderBy clause applied if the condition is true;
    /// otherwise, the original queryable collection.
    /// </returns>
    public static IQueryable<TSource> ApplyOrderBy<TSource, TKey>(
        this IQueryable<TSource> query,
        bool condition,
        Expression<Func<TSource, TKey>> predicate
    ) => condition ? query.OrderBy(predicate) : query;

    public static EntityEntry<T>? AddIfNotExists<T>(
        this DbSet<T> dbSet,
        T entity,
        Expression<Func<T, bool>>? predicate = null
    )
        where T : class
    {
        var exists = predicate != null ? dbSet.Any(predicate) : dbSet.Any();
        return !exists ? dbSet.Add(entity) : null;
    }

    public static IQueryable<TSource> ApplyOrderBy<TSource, TKey>(
        this IQueryable<TSource> query,
        SortDirection direction,
        Expression<Func<TSource, TKey>> predicate
    ) =>
        direction switch
        {
            SortDirection.Asc => query.OrderBy(predicate),
            SortDirection.Desc => query.OrderByDescending(predicate),
            SortDirection.NoSort => query,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };
}
