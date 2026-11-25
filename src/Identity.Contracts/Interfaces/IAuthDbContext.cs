using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Reaparr.Identity.Contracts;

public interface IAuthDbContext : IDataProtectionKeyContext, IDisposable
{
    /// <summary>Gets the change-tracking entry for an entity.</summary>
    /// <param name="entity">The entity to inspect.</param>
    /// <returns>The change-tracking entry for the entity.</returns>
    EntityEntry Entry(object entity);

    /// <summary>Saves all changes made in this context.</summary>
    /// <param name="cancellationToken">A token that cancels the save operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
