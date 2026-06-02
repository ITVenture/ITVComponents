using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels
{
    /// <summary>A purchasable base plan (catalog view).</summary>
    public class PlanViewModel
    {
        public int PlanId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public BillingInterval BillingInterval { get; set; }
        public int? TrialDays { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ProviderPriceId { get; set; }
        public List<string> FeatureKeys { get; set; } = new();
    }

    /// <summary>A purchasable add-on (catalog view).</summary>
    public class AddOnViewModel
    {
        public int AddOnId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public BillingInterval BillingInterval { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ProviderPriceId { get; set; }
        public List<string> FeatureKeys { get; set; } = new();
    }

    /// <summary>One line of the tenant's active subscription.</summary>
    public class SubscriptionItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAddOn { get; set; }
    }

    /// <summary>The current tenant's subscription summary for the manage page.</summary>
    public class SubscriptionOverviewViewModel
    {
        public bool HasSubscription { get; set; }
        public SubscriptionStatus Status { get; set; }
        public System.DateTime? CurrentPeriodEnd { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public List<SubscriptionItemViewModel> Items { get; set; } = new();
    }
}
