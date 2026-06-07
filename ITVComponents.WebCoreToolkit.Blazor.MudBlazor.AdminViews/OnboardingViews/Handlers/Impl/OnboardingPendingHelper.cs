using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Strategy-neutral logic for the deferred direct-onboarding flow. <see cref="StartAsync{TCtx}"/> creates
/// the Identity user (unconfirmed) and parks the requested tenant payload as a <see cref="PendingOnboarding"/>;
/// <see cref="CompleteAsync{TCtx}"/> consumes that record once the user is authenticated, delegating the
/// actual tenant creation back to the active strategy handler so flat and tree share this code.
/// </summary>
internal static class OnboardingPendingHelper
{
    public static async Task<OnboardingStartResult> StartAsync<TCtx, TUser>(TCtx db, UserManager<TUser> userManager,
        OnboardingStartInput input, CancellationToken ct)
        where TCtx : DbContext, IOnboardingPendingContext
        where TUser : IdentityUser, new()
    {
        var user = new TUser { UserName = input.Email, Email = input.Email };
        var result = await userManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            return new OnboardingStartResult(false, null, result.Errors.Select(e => e.Description).ToArray());
        }

        db.PendingOnboardings.Add(new PendingOnboarding
        {
            Email = input.Email,
            PayloadJson = JsonSerializer.Serialize(input.Profile),
            Status = InvitationStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            UserId = user.Id,
            InvitationToken = input.InvitationToken
        });
        await db.SaveChangesAsync(ct);

        return new OnboardingStartResult(true, user.Id, Array.Empty<string>());
    }

    public static async Task<bool> CompleteAsync<TCtx, TUser>(TCtx db, UserManager<TUser> userManager, ClaimsPrincipal principal,
        Func<ClaimsPrincipal, BillingProfileViewModel, CancellationToken, Task<int?>> createTenant, CancellationToken ct)
        where TCtx : DbContext, IOnboardingPendingContext
        where TUser : IdentityUser
    {
        var owner = await userManager.GetUserAsync(principal);
        if (string.IsNullOrEmpty(owner?.Email))
        {
            return false;
        }

        var pending = await db.PendingOnboardings
            .FirstOrDefaultAsync(p => p.Email == owner.Email && p.Status == InvitationStatus.Pending, ct);
        if (pending == null)
        {
            return false;
        }

        var profile = string.IsNullOrEmpty(pending.PayloadJson)
            ? new BillingProfileViewModel()
            : JsonSerializer.Deserialize<BillingProfileViewModel>(pending.PayloadJson) ?? new BillingProfileViewModel();

        var billingProfileId = await createTenant(principal, profile, ct);
        if (billingProfileId == null)
        {
            return false;
        }

        pending.Status = InvitationStatus.Committed;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
