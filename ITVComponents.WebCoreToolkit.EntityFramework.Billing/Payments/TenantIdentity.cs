namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// What is already known about a tenant and its responsible person, in a shape the payment provider can be
    /// told about.
    /// <para>
    /// Everything here is OPTIONAL. It is handed over so the tenant does not have to type it again in the
    /// provider's own onboarding form - not so the platform can decide anything on their behalf. A field left
    /// empty is simply not sent, and the provider asks for it as it did before.
    /// </para>
    /// <para>
    /// Deliberately a flat record and not the billing profile itself: the payments branch stays free of the
    /// onboarding model, the same way it stays free of the tenant-security one.
    /// </para>
    /// </summary>
    public sealed class TenantIdentity
    {
        /// <summary>The registered name of the business. Empty for a one-person tenant.</summary>
        public string? CompanyName { get; set; }

        /// <summary>Telephone number of the business, in whatever shape the tenant entered it.</summary>
        public string? Phone { get; set; }

        /// <summary>VAT / tax number, if the tenant supplied one.</summary>
        public string? VatNumber { get; set; }

        /// <summary>The business address.</summary>
        public PostalAddress? Address { get; set; }

        /// <summary>
        /// The person the provider will ask about - owner or responsible employee. Null when nothing beyond an
        /// e-mail address is known about them, which is the normal case for a company tenant whose owner never
        /// got an employee record.
        /// </summary>
        public ResponsiblePerson? Person { get; set; }
    }

    /// <summary>
    /// An address as the toolkit holds it.
    /// <para>
    /// Note there is no country: the onboarding address model does not carry one. The account country comes
    /// from the payout profile (or the global default) and is the one the provider is told - which is correct
    /// as far as it goes, because the account country IS where the business is established.
    /// </para>
    /// </summary>
    public sealed class PostalAddress
    {
        public string? Street { get; set; }

        /// <summary>House number, held separately from the street in this model.</summary>
        public string? Number { get; set; }

        /// <summary>Additional line (c/o, building, floor).</summary>
        public string? Addition { get; set; }

        public string? Zip { get; set; }

        public string? City { get; set; }
    }

    /// <summary>The individual the provider verifies a business through.</summary>
    public sealed class ResponsiblePerson
    {
        public string? GivenName { get; set; }

        public string? Surname { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }
    }
}
