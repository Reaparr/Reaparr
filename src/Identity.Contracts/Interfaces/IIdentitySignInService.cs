using System.Security.Claims;

namespace Reaparr.Identity.Contracts;

/// <summary>
/// Service interface for sign-in operations.
/// </summary>
public interface IIdentitySignInService
{
    /// <summary>Attempts to sign in a user with username and password.</summary>
    /// <param name="userName">The username to authenticate.</param>
    /// <param name="password">The password to verify.</param>
    /// <param name="isPersistent">Whether the sign-in should persist across browser sessions.</param>
    /// <param name="lockoutOnFailure">Whether failed attempts count toward lockout.</param>
    /// <returns>The result of the sign-in attempt.</returns>
    Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent, bool lockoutOnFailure);

    /// <summary>Signs out the current user.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SignOutAsync();

    /// <summary>Signs in a user with the supplied claims.</summary>
    /// <param name="user">The user to sign in.</param>
    /// <param name="claims">Additional claims to include in the identity.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SignInAsync(AppUser user, IEnumerable<Claim> claims);
}
