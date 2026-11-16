using Microsoft.AspNetCore.Identity;

namespace Reaparr.Identity;

/// <summary>
/// Identity-based implementation of IRoleService
/// </summary>
public class IdentityRoleService : IRoleService
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public IdentityRoleService(RoleManager<IdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    /// <inheritdoc/>
    public async Task<bool> RoleExistsAsync(string roleName) => await _roleManager.RoleExistsAsync(roleName);

    /// <inheritdoc/>
    public async Task<IdentityResult> CreateAsync(IdentityRole role) => await _roleManager.CreateAsync(role);
}
