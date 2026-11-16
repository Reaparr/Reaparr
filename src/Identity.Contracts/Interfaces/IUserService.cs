namespace Reaparr.Identity.Contracts;

/// <summary>
/// Service interface for user management operations
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Finds a user by username
    /// </summary>
    Task<AppUser?> FindByNameAsync(string userName);

    /// <summary>
    /// Finds a user by email
    /// </summary>
    Task<AppUser?> FindByEmailAsync(string email);

    /// <summary>
    /// Creates a new user
    /// </summary>
    Task<IdentityResult> CreateAsync(AppUser user, string password);

    /// <summary>
    /// Gets roles for a user
    /// </summary>
    Task<IList<string>> GetRolesAsync(AppUser user);

    /// <summary>
    /// Gets the first user from the users collection
    /// </summary>
    Task<AppUser?> GetFirstUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user
    /// </summary>
    Task<IdentityResult> UpdateAsync(AppUser user);

    /// <summary>
    /// Validates a password against the configured Identity policy without changing the user.
    /// </summary>
    Task<IdentityResult> ValidatePasswordAsync(AppUser user, string password);

    /// <summary>
    /// Generates a password reset token for a user
    /// </summary>
    Task<string> GeneratePasswordResetTokenAsync(AppUser user);

    /// <summary>
    /// Resets a user's password using a token
    /// </summary>
    Task<IdentityResult> ResetPasswordAsync(AppUser user, string token, string newPassword);

    /// <summary>
    /// Deletes a user
    /// </summary>
    Task<IdentityResult> DeleteAsync(AppUser user);

    /// <summary>
    /// Adds a user to a role
    /// </summary>
    Task<IdentityResult> AddToRoleAsync(AppUser user, string role);

    /// <summary>
    /// Checks if a password is correct for a user
    /// </summary>
    Task<bool> CheckPasswordAsync(AppUser user, string password);

    Task<string> GetTelemetryId();
}
