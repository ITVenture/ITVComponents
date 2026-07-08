using ITVComponents.EFRepo.DataSync;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Configuration
{
    /// <summary>
    /// System-config export section for the billing catalog (plans, add-ons, their links and prices). Contributed
    /// to the global system-configuration via <see cref="IConfigExtension"/>. Deliberately excludes runtime state
    /// (subscriptions) and environment-specific provider ids (Stripe product/price ids) — the latter are
    /// re-established per environment by pushing the catalog to the provider.
    /// </summary>
    [SystemConfigHandler(SectionName, typeof(BillingConfigExtension))]
    public class BillingConfigMarkup : ConfigExtensionMarkup
    {
        public const string SectionName = "billing";

        public BillingAddOnMarkup[] AddOns { get; set; }

        public BillingPlanMarkup[] Plans { get; set; }
    }

    /// <summary>An add-on identity (name + granted feature keys). Pricing lives per plan on <see cref="BillingPlanAddOnMarkup"/>.</summary>
    public class BillingAddOnMarkup
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public string[] Features { get; set; }
    }

    public class BillingPlanMarkup
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public BillingInterval BillingInterval { get; set; }
        public int? TrialDays { get; set; }
        public int? SeatCount { get; set; }
        public bool IsActive { get; set; }
        public BillingPriceMarkup[] Prices { get; set; }
        public string[] Features { get; set; }

        /// <summary>Add-ons bookable with this plan and their per-currency price under this plan.</summary>
        public BillingPlanAddOnMarkup[] AddOns { get; set; }
    }

    public class BillingPlanAddOnMarkup
    {
        public string AddOnName { get; set; }
        public BillingPriceMarkup[] Prices { get; set; }
    }

    /// <summary>A currency-specific amount. Provider price id is intentionally not part of the config.</summary>
    public class BillingPriceMarkup
    {
        public string Currency { get; set; }
        public decimal Amount { get; set; }
    }
}
