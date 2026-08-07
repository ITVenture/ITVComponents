using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options
{
    /// <summary>
    /// Provider-agnostic options for the tenant onboarding flow.
    /// </summary>
    [SettingName("TenantSetup")]
    public class TenantSetupOptions
    {
        /// <summary>
        /// Preferred way to pick the onboarding template: the <c>TenantTypeName</c> of a <c>TenantType</c> whose
        /// attached <c>TenantTemplate</c> is applied to a newly onboarded tenant (and the tenant is tagged with that
        /// type). Takes precedence over <see cref="BasicTenantTemplate"/>. When set but the type is missing or carries
        /// no template, no template is applied (a warning is logged).
        /// </summary>
        public string BasicTenantType { get; set; }

        /// <summary>
        /// Legacy: the <c>Name</c> of the <c>TenantTemplate</c> to apply directly. Used as a fallback only when
        /// <see cref="BasicTenantType"/> is not set. Prefer <see cref="BasicTenantType"/>.
        /// </summary>
        public string BasicTenantTemplate { get; set; }

        public string AdminUserRole { get; set; }

        public string SubscriptionAssetKey { get; set; }

        /// <summary>
        /// Hierarchy scenario only. When <c>false</c>, an onboarded tenant must end up with a parent —
        /// either an explicitly picked/invited one or <see cref="DefaultParentTenant"/>. If neither can be
        /// resolved, onboarding is rejected, so no further root tenants can be created. Defaults to <c>true</c>
        /// (current behaviour: roots may be created freely).
        /// </summary>
        public bool AllowRootTenantCreation { get; set; } = true;

        /// <summary>
        /// Hierarchy scenario only. Optional <c>TenantName</c> (fallback: <c>DisplayName</c>) of a tenant that
        /// newly onboarded tenants are subordinated under when no parent was explicitly picked or invited.
        /// When set, the create-tenant page hides the free parent picker and auto-assigns this parent.
        /// </summary>
        public string DefaultParentTenant { get; set; }

        /// <summary>
        /// When <c>true</c> (recommended for a productive web), creating an <c>EmployeeRoleMapping</c> always
        /// creates a NEW, dedicated tenant role: the role-mappings admin UI hides the "wrap an existing role"
        /// picker and the handler rejects any attempt to link a pre-existing role. This prevents an admin from
        /// accidentally repurposing an existing (permission-bearing) role as a DirectRole. Defaults to
        /// <c>false</c> (existing roles may be wrapped into a mapping).
        /// </summary>
        public bool ForceDedicatedRoleForMappings { get; set; }

        /// <summary>
        /// Darf sich jemand ohne Einladung selbst ein Konto anlegen (<c>/Account/Register</c>)? Vorgabe
        /// <c>false</c> - die Seite weist den Vorgang dann ab und der Verweis auf der Anmeldeseite
        /// verschwindet.
        /// </summary>
        /// <remarks>
        /// Bewusst zurueckhaltend voreingestellt: ein offenes Registrierungsformular ist eine
        /// Entscheidung, die ein Betrieb treffen soll, und nicht etwas, das mit einem Paket-Update
        /// hereinkommt. Der Einladungs-Weg (<c>/Account/Onboarding/JoinRegister</c>) und das
        /// Direkt-Onboarding mit eigenem Mandanten bleiben davon unberuehrt.
        /// </remarks>
        public bool AllowSelfRegistration { get; set; }

        /// <summary>
        /// Der <c>TenantName</c> (ersatzweise <c>DisplayName</c>) des Mandanten, dem ein selbst
        /// registrierter Benutzer zugewiesen wird. Leer = er bekommt keinen.
        /// </summary>
        /// <remarks>
        /// Zugewiesen wird NACH der Mailbestaetigung und nur, wenn der Benutzer noch keinem Mandanten
        /// angehoert und auch keine Einladung auf ihn wartet - eine Einladung hat Vorrang, sie fuehrt ihn
        /// dorthin, wo er hingehoert.
        /// <para>
        /// Ist <see cref="AllowSelfRegistration"/> gesetzt und dies nicht, kann sich zwar jemand
        /// registrieren, landet danach aber ohne Mandanten in einer leeren Uebersicht. Das wird
        /// protokolliert.
        /// </para>
        /// </remarks>
        public string DefaultUserTenant { get; set; }

        /// <summary>
        /// Die Rolle, die ein selbst registrierter Benutzer im <see cref="DefaultUserTenant"/> erhaelt.
        /// Leer = er wird nur Mitglied, ohne Rolle - was in aller Regel bedeutet, dass er nichts sieht.
        /// </summary>
        public string DefaultUserTenantRole { get; set; }
    }
}
