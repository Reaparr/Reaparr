namespace Reaparr.Identity.Contracts;

/// <summary>
/// Service interface for role management operations.
/// </summary>
public interface IRoleService
{
    /// <summary>Checks if a role exists.</summary>
    /// <param name="roleName">The role name to check.</param>
    /// <returns><see langword="true"/> if the role exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> RoleExistsAsync(string roleName);

    /// <summary>Creates a new role.</summary>
    /// <param name="role">The role to create.</param>
    /// <returns>The result of the identity operation.</returns>
    Task<IdentityResult> CreateAsync(IdentityRole role);
}
