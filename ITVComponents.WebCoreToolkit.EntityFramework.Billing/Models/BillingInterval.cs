namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// Billing cadence of a <see cref="Plan"/> or <see cref="AddOn"/>.
    /// </summary>
    public enum BillingInterval
    {
        OneTime,
        Monthly,
        Yearly
    }
}
