using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Liest ALLE Tenants tenant-agnostisch. Gedacht fuer Hintergrund-Dienste, die pro Tenant arbeiten
    /// muessen (z.B. der Workflow-Background-Worker), OHNE eine Per-Tenant-Permission vorauszusetzen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Tenant-Tabelle traegt bewusst keinen Query-Filter (sie ist in keinem <c>GlobalFilterBuilder</c>
    /// registriert), daher liefert das Lesen alle Zeilen - ohne <c>ShowAllTenants</c>-Escalation und ohne
    /// einen Benutzer, der in jedem Tenant eine Permission haette.
    /// </para>
    /// <para>
    /// <b>Registrierung:</b> Das Interface ist <see cref="ExplicitlyExposeAttribute">explizit exponiert</see>
    /// und wird von den konkreten Security-Kontexten implementiert; die bestehende
    /// <c>RegisterExplicityInterfacesScoped</c>-Verdrahtung in den <c>UseDbIdentities</c>-Methoden registriert
    /// es damit automatisch - der Host muss nichts zusaetzlich tun.
    /// </para>
    /// </remarks>
    [ExplicitlyExpose]
    public interface IAllTenantsReader
    {
        /// <summary>
        /// Liefert alle Tenants (Id + Name). Der Name ist der Wert, der als fixierter Scope an einen
        /// Hintergrund-Kontext (<c>PrepareBackgroundContext(user, tenantName)</c>) uebergeben wird.
        /// </summary>
        IReadOnlyList<TenantIdentity> ReadAllTenants();
    }

    /// <summary>Minimale, UI-neutrale Kennung eines Tenants: technischer Schluessel und eindeutiger Name.</summary>
    /// <param name="TenantId">Der Primaerschluessel des Tenants.</param>
    /// <param name="TenantName">Der eindeutige technische Name (dient als fixierter Scope).</param>
    public readonly record struct TenantIdentity(int TenantId, string TenantName);
}
