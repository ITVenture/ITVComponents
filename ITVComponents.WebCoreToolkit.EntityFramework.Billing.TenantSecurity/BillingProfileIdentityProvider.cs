using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity
{
    /// <summary>
    /// Reads what the tenant already entered in its billing profile, so the payment provider can be told rather
    /// than the tenant asked a second time.
    /// <para>
    /// Whoever set up a tenant has typed their company name, address, telephone number and VAT number once
    /// already. Having to type them again into the provider's form - a form in a different language, with
    /// different field names - is exactly the kind of friction that makes people give up halfway through. Every
    /// field handed over here is one that is already filled in when they get there.
    /// </para>
    /// <para>
    /// Flat and hierarchical are separate classes, because the onboarding context interfaces are closed and name
    /// their concrete types. The shared reasoning sits in <see cref="BillingProfileIdentityMapper"/> so the two
    /// cannot drift apart.
    /// </para>
    /// </summary>
    public class BillingProfileIdentityProvider<TContext> : ITenantIdentityProvider
        where TContext : DbContext, ISecurityContextWithOnboarding
    {
        private readonly IDbContextFactory<TContext> dbFactory;

        public BillingProfileIdentityProvider(IDbContextFactory<TContext> dbFactory)
        {
            this.dbFactory = dbFactory;
        }

        /// <inheritdoc />
        public async Task<TenantIdentity?> GetIdentityAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                var profile = await db.BillingProfiles.AsNoTracking().IgnoreQueryFilters()
                    .Include(p => p.DefaultAddress)
                    .Include(p => p.InvoiceAddress)
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
                if (profile == null)
                {
                    return null;
                }

                // Der Eigentuemer hat typischerweise KEINEN Mitarbeiter-Datensatz - wer einen Mandanten anlegt,
                // steht im Profil als Owner. Gibt es trotzdem einen, traegt er den Personennamen, den ein
                // Firmenprofil selbst nicht hat.
                var owner = await db.Employees.AsNoTracking().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.BillingProfileId == profile.BillingProfileId
                                              && e.UserId == profile.OwnerUserId, cancellationToken);

                return BillingProfileIdentityMapper.Map(profile.ProfileType, profile.CompanyName, profile.FirstName,
                    profile.LastName, profile.Email, profile.PhoneNumber, profile.VatNumber,
                    BillingProfileIdentityMapper.Chosen(profile.UseInvoiceAddr,
                        BillingProfileIdentityMapper.Address(profile.DefaultAddress),
                        BillingProfileIdentityMapper.Address(profile.InvoiceAddress)),
                    owner?.FirstName, owner?.LastName, owner?.EMail);
            }
            catch (Exception ex)
            {
                // Vorbelegung ist Bequemlichkeit, keine Voraussetzung: faellt sie aus, entsteht das Konto trotzdem
                // und der Anbieter fragt selbst. Stillschweigen waere hier aber falsch - sonst sucht jemand den
                // Fehler im Anbieter-Formular, waehrend er in der Abfrage liegt.
                LogEnvironment.LogEvent(
                    $"Could not read the billing profile of tenant {tenantId} to pre-fill the payout onboarding; the account is created without it: {ex.OutlineException()}",
                    LogSeverity.Warning, "TenantPayments");
                return null;
            }
        }

    }

    /// <summary>Dasselbe fuer die hierarchische Auspraegung - siehe <see cref="BillingProfileIdentityProvider{TContext}"/>.</summary>
    public class HierarchyBillingProfileIdentityProvider<TContext> : ITenantIdentityProvider
        where TContext : DbContext, IHierarchySecurityContextWithOnboarding
    {
        private readonly IDbContextFactory<TContext> dbFactory;

        public HierarchyBillingProfileIdentityProvider(IDbContextFactory<TContext> dbFactory)
        {
            this.dbFactory = dbFactory;
        }

        /// <inheritdoc />
        public async Task<TenantIdentity?> GetIdentityAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                var profile = await db.BillingProfiles.AsNoTracking().IgnoreQueryFilters()
                    .Include(p => p.DefaultAddress)
                    .Include(p => p.InvoiceAddress)
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
                if (profile == null)
                {
                    return null;
                }

                var owner = await db.Employees.AsNoTracking().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.BillingProfileId == profile.BillingProfileId
                                              && e.UserId == profile.OwnerUserId, cancellationToken);

                return BillingProfileIdentityMapper.Map(profile.ProfileType, profile.CompanyName, profile.FirstName,
                    profile.LastName, profile.Email, profile.PhoneNumber, profile.VatNumber,
                    BillingProfileIdentityMapper.Chosen(profile.UseInvoiceAddr,
                        BillingProfileIdentityMapper.Address(profile.DefaultAddress),
                        BillingProfileIdentityMapper.Address(profile.InvoiceAddress)),
                    owner?.FirstName, owner?.LastName, owner?.EMail);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not read the billing profile of tenant {tenantId} to pre-fill the payout onboarding; the account is created without it: {ex.OutlineException()}",
                    LogSeverity.Warning, "TenantPayments");
                return null;
            }
        }
    }

    /// <summary>
    /// Die Entscheidungen, die fuer beide Auspraegungen dieselben sind. Getrennt gehalten, damit flach und
    /// hierarchisch nicht auseinanderlaufen - sie tun es sonst, und zwar unbemerkt.
    /// </summary>
    internal static class BillingProfileIdentityMapper
    {
        internal static TenantIdentity Map(ProfileType profileType, string? companyName, string? firstName,
            string? lastName, string? email, string? phone, string? vatNumber, PostalAddress? address,
            string? ownerFirstName, string? ownerLastName, string? ownerEmail)
        {
            var identity = new TenantIdentity
            {
                CompanyName = Trimmed(companyName),
                Phone = Trimmed(phone),
                VatNumber = Trimmed(vatNumber),
                Address = address
            };

            // Der Name der verantwortlichen Person, in der Reihenfolge, in der er verlaesslich ist: ein
            // Mitarbeiter-Datensatz des Eigentuemers zuerst, dann - nur bei einem Personal-Profil - die
            // Namensfelder des Profils selbst. Bei einem Firmenprofil beschreiben die die FIRMA, nicht den
            // Menschen; sie dort heranzuziehen hiesse, dem Anbieter eine GmbH als Vornamen zu melden.
            var given = Trimmed(ownerFirstName) ?? (profileType == ProfileType.Personal ? Trimmed(firstName) : null);
            var surname = Trimmed(ownerLastName) ?? (profileType == ProfileType.Personal ? Trimmed(lastName) : null);
            var personEmail = Trimmed(ownerEmail) ?? Trimmed(email);

            if (given != null || surname != null || personEmail != null)
            {
                identity.Person = new ResponsiblePerson
                {
                    GivenName = given,
                    Surname = surname,
                    Email = personEmail,
                    Phone = Trimmed(phone)
                };
            }

            return identity;
        }

        /// <summary>
        /// Uebernimmt eine Adresse, sofern sie ueberhaupt etwas enthaelt. Eine leere Adresse zu senden waere
        /// schlechter als keine: der Anbieter pruefte sie und beanstandete sie.
        /// <para>
        /// Generisch ueber die Basisklasse und nicht ueber Reflexion: flach und hierarchisch haben je einen
        /// eigenen Adress-Typ, teilen aber dieselbe Basis. Ein umbenanntes Feld faellt so beim Uebersetzen auf
        /// und nicht als stumm leere Adresse beim Anbieter.
        /// </para>
        /// </summary>
        /// <summary>
        /// Die Adresse, die gilt: die Rechnungsadresse, wenn der Mandant eine eigene fuehrt, sonst die
        /// Standardadresse. Ist die Rechnungsadresse angehakt aber leer, gilt wieder die Standardadresse -
        /// besser eine Adresse als keine.
        /// <para>
        /// Gewaehlt wird auf der UMGEWANDELTEN Form, nicht auf den Entitaeten: Standard- und Rechnungsadresse
        /// sind im Modell zwei verschiedene Typen (eigene Typparameter am Rechnungsprofil), aus denen sich ein
        /// gemeinsamer nicht ableiten laesst.
        /// </para>
        /// </summary>
        internal static PostalAddress? Chosen(bool useInvoice, PostalAddress? standard, PostalAddress? invoice)
            => useInvoice ? invoice ?? standard : standard;

        internal static PostalAddress? Address<TProfile>(AddressBase<TProfile>? address)
            where TProfile : class
        {
            if (address == null)
            {
                return null;
            }

            var result = new PostalAddress
            {
                Street = Trimmed(address.Street),
                Number = Trimmed(address.Number),
                Addition = Trimmed(address.Addition1),
                Zip = Trimmed(address.Zip),
                City = Trimmed(address.City)
            };

            return result.Street == null && result.Zip == null && result.City == null ? null : result;
        }

        private static string? Trimmed(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
