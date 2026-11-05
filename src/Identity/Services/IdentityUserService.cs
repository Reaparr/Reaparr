using Microsoft.AspNetCore.Identity;

namespace Reaparr.Identity.Services;

/// <summary>
/// Identity-based implementation of IUserService
/// </summary>
public class IdentityUserService : IUserService
{
    private readonly UserManager<AppUser> _userManager;

    public IdentityUserService(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc/>
    public async Task<AppUser?> FindByNameAsync(string userName) => await _userManager.FindByNameAsync(userName);

    /// <inheritdoc/>
    public async Task<AppUser?> FindByEmailAsync(string email) => await _userManager.FindByEmailAsync(email);

    /// <inheritdoc/>
    public async Task<IdentityResult> CreateAsync(AppUser user, string password) =>
        await _userManager.CreateAsync(user, password);

    /// <inheritdoc/>
    public async Task<IList<string>> GetRolesAsync(AppUser user) => await _userManager.GetRolesAsync(user);

    /// <inheritdoc/>
    public async Task<AppUser?> GetFirstUserAsync(CancellationToken cancellationToken = default) =>
        await _userManager.Users.FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IdentityResult> UpdateAsync(AppUser user) => await _userManager.UpdateAsync(user);

    /// <inheritdoc/>
    public async Task<IdentityResult> ValidatePasswordAsync(AppUser user, string password)
    {
        List<IdentityError>? errors = null;
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, password);
            if (!result.Succeeded)
            {
                errors ??= [];
                errors.AddRange(result.Errors);
            }
        }

        return errors is null ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
    }

    /// <inheritdoc/>
    public async Task<string> GeneratePasswordResetTokenAsync(AppUser user) =>
        await _userManager.GeneratePasswordResetTokenAsync(user);

    /// <inheritdoc/>
    public async Task<IdentityResult> ResetPasswordAsync(AppUser user, string token, string newPassword) =>
        await _userManager.ResetPasswordAsync(user, token, newPassword);

    /// <inheritdoc/>
    public async Task<IdentityResult> DeleteAsync(AppUser user) => await _userManager.DeleteAsync(user);

    /// <inheritdoc/>
    public async Task<IdentityResult> AddToRoleAsync(AppUser user, string role) =>
        await _userManager.AddToRoleAsync(user, role);

    /// <inheritdoc/>
    public async Task<bool> CheckPasswordAsync(AppUser user, string password) =>
        await _userManager.CheckPasswordAsync(user, password);

    public async Task<string> GetTelemetryId() =>
        (await _userManager.Users.FirstOrDefaultAsync(CancellationToken.None))?.Id ?? string.Empty;
}
