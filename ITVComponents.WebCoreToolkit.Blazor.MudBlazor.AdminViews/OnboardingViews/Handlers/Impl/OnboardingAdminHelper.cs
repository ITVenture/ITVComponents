using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Der strategie-neutrale Teil der Onboarding-Verwaltung - alles, was der flache und der hierarchische
/// Handler <b>wortgleich</b> taten, ohne dass ein strategie-eigener Entitaetstyp daran haengt.
/// </summary>
/// <remarks>
/// <para>
/// Dasselbe Muster wie <see cref="OnboardingPendingHelper"/>: statische Methoden mit <b>engen</b>
/// Typparametern je Methode statt einer gemeinsamen Basisklasse. Der Grund steht in der Review-Doku:
/// die beiden Handler sind zwar zu 96,6 % textgleich, arbeiten aber auf disjunkten Typen
/// (<c>Role</c>, <c>RoleRole</c>, <c>User</c>, <c>Tenant</c>, <c>TenantUser</c> und die
/// Onboarding-Entitaeten gibt es je Strategie einmal, mit je eigener generischer Basis). Eine
/// gemeinsame Basisklasse braeuchte rund neunzehn Typparameter - ein Drittel davon nur, um die
/// Constraint-Kette zu schliessen. Was hier steht, ist der Teil, der ohne diesen Preis zu haben ist.
/// </para>
/// <para>
/// Die Auswahlregel ist einfach: was eine <b>Entscheidung</b> trifft (Permission, Feature-Gate,
/// Namensfindung) oder auf einem <b>geteilten</b> Typ arbeitet (<see cref="ConsentRecord"/>,
/// <c>Feature</c>, <see cref="AddressBase{TBillingProfile}"/>), gehoert hierher. Die getippten Abfragen
/// auf den strategie-eigenen Entitaeten bleiben im jeweiligen Handler - dort kostet der Typ nichts.
/// </para></remarks>
internal static class OnboardingAdminHelper
{
    /// <summary>
    /// Loest den Mandanten des aktuellen Bereichs auf und setzt den Admin-Riegel durch: eine
    /// <c>CurrentTenantId</c> ungleich null plus IRGENDEINE der verlangten Berechtigungen.
    /// </summary>
    /// <param name="db">der Kontext - nur wegen <see cref="ITenantScopeContext.CurrentTenantId"/></param>
    /// <param name="services">der Dienste-Anbieter fuer die Berechtigungspruefung</param>
    /// <param name="requiredAnyOf">
    /// die zulaessigen Berechtigungen; leer = "irgendeine Onboarding-Admin-Berechtigung"
    /// </param>
    /// <remarks>
    /// Die Pruefung laeuft gegen den aktuellen Berechtigungs-Bereich, und die zentrale Autorisierung
    /// beschraenkt den bereits auf den - moeglicherweise geerbten - freigeschalteten Zugang des Aufrufers
    /// zu diesem Mandanten. Eine eigene Mitgliedschafts-Sonde waere daher nicht nur ueberfluessig, sondern
    /// falsch: sie wiese einen Aufrufer ab, dessen Berechtigung von einem uebergeordneten Mandanten kommt.
    /// <para>
    /// Genau deshalb steht die Methode hier und nicht zweimal: der Unterschied zwischen den Strategien lag
    /// bisher nur im Kommentar, nicht im Code.
    /// </para></remarks>
    public static (bool ok, int tenantId) Authorize(ITenantScopeContext db, IServiceProvider services,
        params string[] requiredAnyOf)
    {
        var current = db.CurrentTenantId ?? 0;
        var required = requiredAnyOf is { Length: > 0 } ? requiredAnyOf : OnboardingAdminPermissions.AnyAccess;
        return current == 0 || !services.VerifyUserPermissions(required) ? (false, 0) : (true, current);
    }

    /// <summary>
    /// Muss eine Zuordnung ihre Rolle zwingend selbst mitbringen? Vorgabe aus den
    /// <see cref="TenantSetupOptions"/>; ohne Einstellung false.
    /// </summary>
    public static bool ForceDedicatedRoleForMappings(IServiceProvider services)
        => services.GetService<IGlobalSettings<TenantSetupOptions>>()?.ValueOrDefault?.ForceDedicatedRoleForMappings
           ?? false;

    /// <summary>
    /// Die Berechtigung, die das <b>Anlegen, Aendern und Loeschen</b> einer Zuordnung dieser Art verlangt.
    /// Bei <see cref="EmployeeRoleMappingKind.Delegation"/> ist das die volle Bearbeitungs-Stufe - die
    /// schwaechere Zuweis-Stufe darf ausdruecklich nicht anlegen oder umbenennen.
    /// </summary>
    public static string[] RequiredWritePermission(EmployeeRoleMappingKind kind)
        => kind switch
        {
            EmployeeRoleMappingKind.DirectRole => OnboardingAdminPermissions.DirectRoleWrite,
            EmployeeRoleMappingKind.PermissionSet => OnboardingAdminPermissions.PermissionSetWrite,
            EmployeeRoleMappingKind.Delegation => OnboardingAdminPermissions.DelegationWrite,
            _ => OnboardingAdminPermissions.RoleMappingsAnyWrite
        };

    /// <summary>
    /// Die Berechtigung, die das <b>Aktivieren eines Berechtigungs-Satzes</b> auf dieser Eltern-Zuordnung
    /// verlangt. Auf einer Delegations-Rolle genuegt die schwaechere Zuweis-Stufe - das ist der ganze Sinn
    /// der Art <see cref="EmployeeRoleMappingKind.Delegation"/>.
    /// </summary>
    public static string[] RequiredAssignPermission(EmployeeRoleMappingKind parentKind)
        => parentKind == EmployeeRoleMappingKind.Delegation
            ? OnboardingAdminPermissions.DelegationAssign
            : OnboardingAdminPermissions.DirectRoleWrite;

    /// <summary>Darf der Aufrufer das Sichtbarkeits-Gate uebergehen (und ein Gate ueberhaupt setzen)?</summary>
    public static bool MayIgnoreFeatureGate(IServiceProvider services)
        => services.VerifyUserPermissions(OnboardingAdminPermissions.AllFeatures);

    /// <summary>
    /// Die im Bereich des Aufrufers (also im aktuellen Mandanten) aktivierten Features - dieselbe Quelle,
    /// aus der auch <c>SecureView</c> und <c>VerifyActivatedFeatures</c> lesen.
    /// </summary>
    public static List<string> ActiveFeatureNames(IServiceProvider services)
    {
        var scope = services.GetService<IPermissionScope>();
        var repo = services.GetService<ISecurityRepository>();
        if (scope == null || repo == null)
        {
            return new List<string>();
        }

        return repo.GetFeatures(scope.PermissionPrefix)
            .Where(f => f.Enabled)
            .Select(f => f.FeatureName)
            .ToList();
    }

    /// <summary>
    /// Der Feature-Katalog. Steht hier vollstaendig, weil <c>Feature</c> ein GETEILTER Typ ist - beide
    /// Strategien lesen dieselbe Tabelle, und der Zugriff laeuft ohnehin ueber <c>db.Set&lt;&gt;()</c>.
    /// </summary>
    public static Task<FeatureOption[]> ListFeaturesAsync(DbContext db, CancellationToken ct)
        => db.Set<ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Feature>()
            .AsNoTracking()
            .OrderBy(f => f.FeatureName)
            .Select(f => new FeatureOption
            {
                FeatureName = f.FeatureName,
                FeatureDescription = f.FeatureDescription,
                Enabled = f.Enabled
            })
            .ToArrayAsync(ct);

    /// <summary>
    /// Die Zustimmungs-Nachweise, die den Mandanten etwas angehen. Steht hier, weil
    /// <see cref="ConsentRecord"/> strategie-neutral ist - eine Zustimmung hat mit der Mandanten-Strategie
    /// nichts zu tun (siehe <see cref="IOnboardingConsentContext"/>).
    /// </summary>
    /// <param name="db">der Kontext - nur der neutrale Ausschnitt</param>
    /// <param name="tenantId">der Mandant, um den es geht</param>
    /// <param name="memberUserIds">
    /// die Mitglieder dieses Mandanten. Der Aufrufer ermittelt sie, weil die Mitgliedschafts-Tabelle je
    /// Strategie einen eigenen Typ hat - und weil nur er weiss, wie weit sein Mandantenbegriff reicht.
    /// </param>
    /// <param name="ct">das Abbruch-Token</param>
    /// <remarks>
    /// Zwei Arten von Nachweisen laufen hier zusammen: die des Mandanten (Tenant/Both, mit TenantId) und die
    /// persoenlichen seiner Mitglieder (ohne TenantId). Ein Nachweis einer Person, die dem Mandanten nicht
    /// angehoert, ist nie dabei.
    /// </remarks>
    public static Task<ConsentRecordViewModel[]> ListConsentsAsync(IOnboardingConsentContext db, int tenantId,
        List<string> memberUserIds, CancellationToken ct)
        => db.ConsentRecords.AsNoTracking()
            .Where(r => r.TenantId == tenantId
                        || (r.TenantId == null && r.UserId != null && memberUserIds.Contains(r.UserId)))
            .OrderByDescending(r => r.AcceptedUtc)
            .Select(r => new ConsentRecordViewModel
            {
                ConsentRecordId = r.ConsentRecordId,
                ConsentKey = r.ConsentKey,
                Version = r.Version,
                Accepted = r.Accepted,
                AcceptedUtc = r.AcceptedUtc,
                Email = r.Email,
                Scope = r.Scope,
                TenantId = r.TenantId,
                Culture = r.Culture,
                Origin = r.Origin
            })
            .ToArrayAsync(ct);

    /// <summary>
    /// Sucht einen freien Rollennamen: den Wunschnamen, sonst <c>name_1</c> … <c>name_5</c>. Liefert null,
    /// wenn der Wunschname und alle fuenf Varianten vergeben sind - dann scheitert das Speichern.
    /// </summary>
    /// <param name="baseName">der Wunschname</param>
    /// <param name="isTakenAsync">
    /// prueft, ob ein Name im Mandanten schon vergeben ist. Als Rueckruf, weil die Rollentabelle je
    /// Strategie einen eigenen Typ hat - die Abfrage bleibt beim Aufrufer, die Namensregel steht hier.
    /// </param>
    public static async Task<string?> FindFreeRoleNameAsync(string baseName, Func<string, Task<bool>> isTakenAsync)
    {
        for (var i = 0; i <= 5; i++)
        {
            var candidate = i == 0 ? baseName : $"{baseName}_{i}";
            if (!await isTakenAsync(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Liest eine Adresse in das Eingabemodell der Maske. Genuegt <see cref="AddressBase{TBillingProfile}"/>:
    /// alle gelesenen Felder stehen dort, der Typparameter haengt nur an der Navigation.
    /// </summary>
    public static AddressInput ToInput<TBillingProfile>(AddressBase<TBillingProfile>? address)
        where TBillingProfile : class
        => address == null
            ? new AddressInput()
            : new AddressInput
            {
                Name = address.Name,
                Addition1 = address.Addition1,
                Addition2 = address.Addition2,
                Street = address.Street,
                Number = address.Number,
                Zip = address.Zip,
                City = address.City
            };

    /// <summary>
    /// Traegt die Eingaben in eine bestehende Adresszeile ein. Das <b>Anlegen</b> bleibt beim Aufrufer: nur
    /// er kennt den strategie-eigenen Adresstyp, den er anlegen muss.
    /// </summary>
    /// <param name="target">die Zeile, die beschrieben wird</param>
    /// <param name="input">die Eingaben der Maske</param>
    /// <param name="fallbackName">
    /// der Name, der einspringt, wenn die Maske keinen liefert - eine Adresse ohne Namen waere in der
    /// Rechnungsstellung nicht zuzuordnen
    /// </param>
    public static void Fill<TBillingProfile>(AddressBase<TBillingProfile> target, AddressInput input,
        string fallbackName)
        where TBillingProfile : class
    {
        target.Name = string.IsNullOrWhiteSpace(input.Name) ? fallbackName : input.Name;
        target.Addition1 = input.Addition1;
        target.Addition2 = input.Addition2;
        target.Street = input.Street;
        target.Number = input.Number;
        target.Zip = input.Zip ?? string.Empty;
        target.City = input.City ?? string.Empty;
    }
}
