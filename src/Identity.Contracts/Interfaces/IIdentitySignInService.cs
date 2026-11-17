using System.Security.Claims;

namespace Reaparr.Identity.Contracts;

/// <summary>
/// Service interface for sign-in operations
/// </summary>
public interface IIdentitySignInService
{
    /// <summary>
    /// Attempts to sign in a user with username and password
    /// </summary>
    Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent, bool lockoutOnFailure);

    /// <summary>
    /// Signs out the current user
    /// </summary>
    Task SignOutAsync();

    Task SignInAsync(AppUser user, IEnumerable<Claim> claims);
}
