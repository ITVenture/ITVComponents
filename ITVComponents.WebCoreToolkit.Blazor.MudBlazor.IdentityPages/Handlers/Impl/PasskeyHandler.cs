using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Handlers.Impl;

/// <summary>
/// Feature-off fallback. Used when no generic <see cref="PasskeyHandler{TUser}"/> is wired up — the Login page
/// hides the Passkey button (UsePage gate), the Manage pages render "not available". Mirrors the LoginHandler
/// fallback pattern from MVC ANCIP.
/// </summary>
internal class PasskeyHandler : IPasskeyHandler
{
    private const string NotEnabledMessage = "Passkeys are not enabled.";

    private static readonly IdentityError NotEnabledIdentityError = new()
    {
        Code = "PasskeysNotEnabled",
        Description = NotEnabledMessage
    };

    public bool UsePage => false;
    public int MaxPasskeysPerUser => 0;

    public Task<string> MakePasskeyRequestOptionsAsync(string? username) => Task.FromResult("{}");

    public Task<SignInResult> PasskeySignInAsync(string credentialJson) => Task.FromResult(SignInResult.Failed);

    public Task<string?> MakePasskeyCreationOptionsAsync(ClaimsPrincipal currentUser) => Task.FromResult<string?>(null);

    public Task<PasskeyAttestationResult> PerformPasskeyAttestationAsync(string credentialJson) =>
        Task.FromResult(PasskeyAttestationResult.Fail(new PasskeyException(NotEnabledMessage)));

    public Task<IList<UserPasskeyInfo>> GetUserPasskeysAsync(ClaimsPrincipal currentUser) =>
        Task.FromResult<IList<UserPasskeyInfo>>(Array.Empty<UserPasskeyInfo>());

    public Task<UserPasskeyInfo?> GetUserPasskeyAsync(ClaimsPrincipal currentUser, byte[] credentialId) =>
        Task.FromResult<UserPasskeyInfo?>(null);

    public Task<IdentityResult> AddOrUpdateUserPasskeyAsync(ClaimsPrincipal currentUser, UserPasskeyInfo passkey) =>
        Task.FromResult(IdentityResult.Failed(NotEnabledIdentityError));

    public Task<IdentityResult> RemoveUserPasskeyAsync(ClaimsPrincipal currentUser, byte[] credentialId) =>
        Task.FromResult(IdentityResult.Failed(NotEnabledIdentityError));
}

/// <summary>
/// Generic implementation that wraps <see cref="SignInManager{TUser}"/> + <see cref="UserManager{TUser}"/>
/// extensions introduced with Identity 10. Register via
/// <c>services.ConfigureHandlerType&lt;PasskeyPageModel, IPasskeyHandler, PasskeyHandler&lt;TUser&gt;&gt;()</c>
/// or rely on the WebPartManager generics dispatch.
/// </summary>
[FallbackPageHandler(typeof(PasskeyHandler))]
internal class PasskeyHandler<TUser> : IPasskeyHandler where TUser : class
{
    private readonly SignInManager<TUser> signInManager;
    private readonly UserManager<TUser> userManager;

    public PasskeyHandler(SignInManager<TUser> signInManager, UserManager<TUser> userManager)
    {
        this.signInManager = signInManager;
        this.userManager = userManager;
    }

    public bool UsePage => true;
    public int MaxPasskeysPerUser => 100;

    public async Task<string> MakePasskeyRequestOptionsAsync(string? username)
    {
        var user = string.IsNullOrEmpty(username) ? null : await userManager.FindByNameAsync(username);
        return await signInManager.MakePasskeyRequestOptionsAsync(user);
    }

    public Task<SignInResult> PasskeySignInAsync(string credentialJson) =>
        signInManager.PasskeySignInAsync(credentialJson);

    public async Task<string?> MakePasskeyCreationOptionsAsync(ClaimsPrincipal currentUser)
    {
        var user = await userManager.GetUserAsync(currentUser);
        if (user is null)
        {
            return null;
        }

        var userId = await userManager.GetUserIdAsync(user);
        var userName = await userManager.GetUserNameAsync(user) ?? "User";
        return await signInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = userId,
            Name = userName,
            DisplayName = userName
        });
    }

    public Task<PasskeyAttestationResult> PerformPasskeyAttestationAsync(string credentialJson) =>
        signInManager.PerformPasskeyAttestationAsync(credentialJson);

    public async Task<IList<UserPasskeyInfo>> GetUserPasskeysAsync(ClaimsPrincipal currentUser)
    {
        var user = await userManager.GetUserAsync(currentUser);
        return user is null ? Array.Empty<UserPasskeyInfo>() : await userManager.GetPasskeysAsync(user);
    }

    public async Task<UserPasskeyInfo?> GetUserPasskeyAsync(ClaimsPrincipal currentUser, byte[] credentialId)
    {
        var user = await userManager.GetUserAsync(currentUser);
        return user is null ? null : await userManager.GetPasskeyAsync(user, credentialId);
    }

    public async Task<IdentityResult> AddOrUpdateUserPasskeyAsync(ClaimsPrincipal currentUser, UserPasskeyInfo passkey)
    {
        var user = await userManager.GetUserAsync(currentUser);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "User not found." });
        }

        return await userManager.AddOrUpdatePasskeyAsync(user, passkey);
    }

    public async Task<IdentityResult> RemoveUserPasskeyAsync(ClaimsPrincipal currentUser, byte[] credentialId)
    {
        var user = await userManager.GetUserAsync(currentUser);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "User not found." });
        }

        return await userManager.RemovePasskeyAsync(user, credentialId);
    }
}
