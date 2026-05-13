using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Handlers;

/// <summary>
/// Handler that abstracts the ASP.NET Core Identity passkey APIs (<see cref="SignInManager{TUser}"/> and
/// <see cref="UserManager{TUser}"/> extensions introduced with Identity 10) for the Blazor IdentityPages library.
/// Services both the sign-in path (<c>Account/Login</c>) and the management path
/// (<c>Account/Manage/Passkeys</c> + <c>Account/Manage/RenamePasskey</c>).
/// </summary>
public interface IPasskeyHandler : IPageHandlerInstance<PasskeyPageModel>
{
    /// <summary>
    /// Upper bound for passkeys per user — the Manage/Passkeys page surfaces a guard banner when reached.
    /// Implementations may return 0 to disable passkey management entirely.
    /// </summary>
    int MaxPasskeysPerUser { get; }

    // ---- Sign-in path (anonymous OK) -------------------------------------------------------------------

    Task<string> MakePasskeyRequestOptionsAsync(string? username);

    Task<SignInResult> PasskeySignInAsync(string credentialJson);

    // ---- Management path (require authenticated user) --------------------------------------------------

    /// <summary>
    /// Returns <c>null</c> when the current principal cannot be resolved to a user (endpoint translates to 404).
    /// </summary>
    Task<string?> MakePasskeyCreationOptionsAsync(ClaimsPrincipal currentUser);

    Task<PasskeyAttestationResult> PerformPasskeyAttestationAsync(string credentialJson);

    Task<IList<UserPasskeyInfo>> GetUserPasskeysAsync(ClaimsPrincipal currentUser);

    Task<UserPasskeyInfo?> GetUserPasskeyAsync(ClaimsPrincipal currentUser, byte[] credentialId);

    Task<IdentityResult> AddOrUpdateUserPasskeyAsync(ClaimsPrincipal currentUser, UserPasskeyInfo passkey);

    Task<IdentityResult> RemoveUserPasskeyAsync(ClaimsPrincipal currentUser, byte[] credentialId);
}
