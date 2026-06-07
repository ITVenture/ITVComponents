using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared
{
    /// <summary>
    /// Strategy-neutral slice of the onboarding context that exposes the pre-tenant
    /// <see cref="PendingOnboarding"/> set. Both the flat and the tree onboarding context interfaces
    /// extend this, so the deferred direct-onboarding flow works against either strategy.
    /// </summary>
    public interface IOnboardingPendingContext
    {
        DbSet<PendingOnboarding> PendingOnboardings { get; set; }
    }
}
