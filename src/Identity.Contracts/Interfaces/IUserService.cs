namespace Reaparr.Identity.Contracts;

/// <summary>
/// Service interface for user management operations.
/// </summary>
public interface IUserService
{
    /// <summary>Finds a user by username.</summary>
    /// <param name="userName">The username to search for.</param>
    /// <returns>The matching user, or <see langword="null"/> if no user exists.</returns>
    Task<AppUser?> FindByNameAsync(string userName);

    /// <summary>Finds a user by email.</summary>
    /// <param name="email">The email address to search for.</param>
    /// <returns>The matching user, or <see langword="null"/> if no user exists.</returns>
    Task<AppUser?> FindByEmailAsync(string email);

    /// <summary>Creates a new user.</summary>
    /// <param name="user">The user to create.</param>
    /// <param name="password">The user's initial password.</param>
    /// <returns>The result of the identity operation.</returns>
    Task<IdentityResult> CreateAsync(AppUser user, string password);

    /// <summary>Gets roles for a user.</summary>
    /// <param name="user">The user whose roles are requested.</param>
    /// <returns>The role names assigned to the user.</returns>
    Task<IList<string>> GetRolesAsync(AppUser user);

    /// <summary>Gets the first user from the users collection.</summary>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>The first user, or <see langword="null"/> if no user exists.</returns>
    Task<AppUser?> GetFirstUserAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates a user.</summary>
    /// <param name="user">The user to update.</param>
    /// <returns>The result of the identity operation.</returns>
    Task<IdentityResult> UpdateAsync(AppUser user);

    /// <summary>Validates a password against the configured Identity policy without changing the user.</summary>
    /// <param name="user">The user whose password is being validated.</param>
    /// <param name="password">The password to validate.</param>
    /// <returns>The validation result.</returns>
    Task<IdentityResult> ValidatePasswordAsync(AppUser user, string password);

    /// <summary>Generates a password reset token for a user.</summary>
    /// <param name="user">The user for whom the token is generated.</param>
    /// <returns>The password reset token.</returns>
    Task<string> GeneratePasswordResetTokenAsync(AppUser user);

    /// <summary>Resets a user's password using a token.</summary>
    /// <param name="user">The user whose password is reset.</param>
    /// <param name="token">The password reset token.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>The result of the identity operation.</returns>
    Task<IdentityResult> ResetPasswordAsync(AppUser user, string token, string newPassword);

    /// <summary>Deletes a user.</summary>
    /// <param name="user">The user to delete.</param>
    /// <returns>The result of the identity operation.</returns>
    Task<IdentityResult> DeleteAsync(AppUser user);

    /// <summary>Adds a user to a role.</summary>
    /// <param name="user">The user to add to a role.</param>
    /// <param name="role">The role name.</param>
    /// <returns>The result of the identity operation.</returns>
    Task<IdentityResult> AddToRoleAsync(AppUser user, string role);

    /// <summary>Checks if a password is correct for a user.</summary>
    /// <param name="user">The user whose password is checked.</param>
    /// <param name="password">The password to check.</param>
    /// <returns><see langword="true"/> when the password matches; otherwise, <see langword="false"/>.</returns>
    Task<bool> CheckPasswordAsync(AppUser user, string password);

    /// <summary>Gets the telemetry identifier for a user.</summary>
    /// <returns>The user's telemetry identifier.</returns>
    Task<string> GetTelemetryId();
}
