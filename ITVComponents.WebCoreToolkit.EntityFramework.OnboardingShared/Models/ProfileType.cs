namespace ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models
{
    /// <summary>
    /// Discriminator for a <c>BillingProfile</c>: a personal-tenant profile (single
    /// natural person registers their own tenant) vs. a company-tenant profile
    /// (organisation with one or more employees).
    /// </summary>
    public enum ProfileType
    {
        Personal = 0,
        Company = 1
    }
}
