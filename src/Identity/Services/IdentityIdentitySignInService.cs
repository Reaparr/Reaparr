using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Reaparr.Identity;

/// <summary>
/// Identity-based implementation of ISignInService
/// </summary>
public class IdentityIdentitySignInService : IIdentitySignInService
{
    private readonly SignInManager<AppUser> _signInManager;

    public IdentityIdentitySignInService(SignInManager<AppUser> signInManager)
    {
        _signInManager = signInManager;
        _signInManager.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    }

    /// <inheritdoc/>
    public async Task<SignInResult> PasswordSignInAsync(
        string userName,
        string password,
        bool isPersistent,
        bool lockoutOnFailure
    ) => await _signInManager.PasswordSignInAsync(userName, password, isPersistent, lockoutOnFailure);

    /// <inheritdoc/>
    public async Task SignInAsync(AppUser user, IEnumerable<Claim> claims)
    {
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, additionalClaims: claims);
    }

    /// <inheritdoc/>
    public async Task SignOutAsync()
    {
        await _signInManager.SignOutAsync();
    }
}
