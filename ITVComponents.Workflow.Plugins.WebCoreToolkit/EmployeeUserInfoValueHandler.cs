using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using CustomUserProperty = ITVComponents.WebCoreToolkit.Models.CustomUserProperty;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit
{
    /// <summary>
    /// Der Benutzer-Handler fuer Umgebungen <b>mit</b> Onboarding: liefert zusaetzlich den
    /// Mitarbeiter-Datensatz und das persoenliche Rechnungsprofil und laesst sich auch ueber eine
    /// Mitarbeiter- oder eine Rechnungsprofil-Id fragen.
    /// </summary>
    /// <remarks>
    /// Bewusst ein eigener Typ und kein Schalter am Basis-Handler: eine Umgebung ohne Onboarding hat
    /// keinen Mitarbeiter-Typ, den sie fuer <c>TEmployee</c> eintragen koennte. Mit einem Schalter
    /// muesste sie dort einen Platzhalter hinterlegen - und der erste, der das falsch macht, sucht lange.
    /// <para>
    /// <c>TEmployee</c> laesst sich <b>nicht</b> aus dem <c>$$genericArgumentProvider</c> ableiten: die
    /// Onboarding-Kontext-Schnittstelle ist geschlossen und traegt keine Typparameter, und die Ableitung
    /// laeuft ohnehin nur ueber Interfaces, nicht ueber Basisklassen. Der Mitarbeiter-Typ braucht deshalb
    /// eine eigene Zeile:
    /// </para>
    /// <code>
    /// $$genericArgumentProvider -> &lt;der konkrete SecurityContext&gt;   (TUser, TTenantUser, TUserProperty)
    /// TEmployee                 -> …Onboarding.Flat.Models.Employee
    /// TBillingProfile           -> …Onboarding.Flat.Models.BillingProfile
    /// </code>
    /// <para>
    /// <b>Warum auch das Rechnungsprofil:</b> wer einen Mandanten anlegt, wird darin als
    /// <c>OwnerUser</c> hinterlegt - ein Mitarbeiter-Datensatz entsteht fuer ihn <b>nicht</b>. Ohne
    /// diesen Weg blieben Vor- und Nachname ausgerechnet bei der Person leer, der der Mandant gehoert.
    /// Herangezogen werden nur Profile vom Typ <c>Personal</c>; bei einem Firmenprofil beschreiben die
    /// Namensfelder die Firma bzw. eine Kontaktperson, nicht diesen Benutzer.
    /// </para>
    /// <para>
    /// <c>TBillingProfile</c> braucht - wie <c>TEmployee</c> - eine eigene Zeile: abgeleitet wird nur
    /// ueber Interfaces, und die Onboarding-Kontext-Schnittstelle ist geschlossen und traegt keine
    /// Typparameter.
    /// </para>
    /// </remarks>
    /// <typeparam name="TUser">der konkrete Benutzer-Typ</typeparam>
    /// <typeparam name="TTenantUser">der konkrete Mandanten-Benutzer-Typ</typeparam>
    /// <typeparam name="TUserProperty">der konkrete Typ der benutzerdefinierten Eigenschaften</typeparam>
    /// <typeparam name="TEmployee">der konkrete Mitarbeiter-Typ</typeparam>
    /// <typeparam name="TBillingProfile">der konkrete Rechnungsprofil-Typ</typeparam>
    public class EmployeeUserInfoValueHandler<TUser, TTenantUser, TUserProperty, TEmployee, TBillingProfile>
        : UserInfoValueHandler<TUser, TTenantUser, TUserProperty>
        where TUser : class
        where TTenantUser : class
        where TUserProperty : CustomUserProperty
        where TEmployee : class
        where TBillingProfile : class
    {
        /// <summary>
        /// Initialisiert den Handler.
        /// </summary>
        /// <param name="contextFactory">die Fabrik, die je Aufruf einen frischen Kontext leiht</param>
        public EmployeeUserInfoValueHandler(IToolkitContextFactory contextFactory) : base(contextFactory)
        {
        }

        /// <summary>
        /// Initialisiert den Handler.
        /// </summary>
        /// <param name="contextFactory">die Fabrik, die je Aufruf einen frischen Kontext leiht</param>
        /// <param name="allowMissing">ob „kein Benutzer" hier ein normaler Zustand ist</param>
        public EmployeeUserInfoValueHandler(IToolkitContextFactory contextFactory, bool allowMissing)
            : base(contextFactory, allowMissing)
        {
        }

        /// <inheritdoc/>
        protected override bool SupportsEmployees => true;

        /// <inheritdoc/>
        protected override bool SupportsBillingProfiles => true;

        /// <inheritdoc/>
        protected override object EmployeeByKey(DbContext db, object employeeId)
        {
            return ByKey<TEmployee>(db, employeeId);
        }

        /// <inheritdoc/>
        protected override object EmployeeByUser(DbContext db, object userKey)
        {
            return FirstWhere<TEmployee>(db, "UserId", userKey);
        }

        /// <inheritdoc/>
        protected override object OwnerProfileByKey(DbContext db, object billingProfileId)
        {
            return ByKey<TBillingProfile>(db, billingProfileId);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Ein Benutzer kann mehrere Profile besitzen (ein persoenliches und die Firmenprofile seiner
        /// Mandanten). Gesucht ist das erste <c>Personal</c>-Profil; der Typ wird im Speicher geprueft,
        /// weil ein <c>WHERE</c> auf das Enum dessen konkreten Typ braeuchte - und es sind ohnehin nur
        /// eine Handvoll Zeilen.
        /// </remarks>
        protected override object OwnerProfileByUser(DbContext db, object userKey)
        {
            IQueryable<TBillingProfile> candidates = Where<TBillingProfile>(db, "OwnerUserId", userKey);
            if (candidates == null)
            {
                return null;
            }

            foreach (TBillingProfile candidate in candidates)
            {
                if (IsPersonalProfile(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
