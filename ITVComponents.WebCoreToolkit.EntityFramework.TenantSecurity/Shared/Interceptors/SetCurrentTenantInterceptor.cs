using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DataAnnotation;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using BinderTenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.BinderModels.BinderTenant;
using HierarchyBinderTenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.BinderModels.BinderTenant;
using HierarchyTenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.HierarchyTenant;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Interceptors
{
    /// <summary>
    /// Traegt beim Speichern den <b>aktuellen Mandanten</b> in alle Eigenschaften ein, die mit
    /// <see cref="AssignTenantAttribute"/> ausgezeichnet sind und beim Anlegen noch leer stehen.
    /// </summary>
    /// <remarks>
    /// Die Ausbaustufe des Mandanten-Modells (flach oder hierarchisch, vollstaendig oder Binder) ist dem
    /// Interceptor <b>gleichgueltig</b>: er braucht davon nur „Name rein, Id raus". Genau das ist die
    /// Nachschlage-Funktion, die er im Konstruktor bekommt - ueber
    /// <see cref="SetTenantType"/> fuer die vier mitgelieferten Auspraegungen, oder als eigene Funktion
    /// (<see cref="TenantIdLookup.For{TTenant}"/>) fuer eine mitgebrachte Entitaet.
    /// </remarks>
    public class SetCurrentTenantInterceptor : SaveChangesInterceptor
    {
        private readonly IServiceProvider services;
        private readonly Func<DbContext, string, int> resolveTenantId;

        /// <summary>
        /// Erzeugt den Interceptor fuer eine der mitgelieferten Mandanten-Auspraegungen.
        /// </summary>
        /// <param name="services">der Dienst-Anbieter (fuer den Berechtigungs-Scope)</param>
        /// <param name="tenantType">welche Mandanten-Entitaet dieser Kontext fuehrt</param>
        public SetCurrentTenantInterceptor(IServiceProvider services, SetTenantType tenantType)
            : this(services, LookupFor(tenantType))
        {
        }

        /// <summary>
        /// Erzeugt den Interceptor mit einer <b>eigenen</b> Nachschlage-Funktion - fuer Kontexte, deren
        /// Mandanten-Entitaet diese Bibliothek nicht kennt.
        /// </summary>
        /// <param name="services">der Dienst-Anbieter (fuer den Berechtigungs-Scope)</param>
        /// <param name="resolveTenantId">Kontext und Mandantenname rein, Mandanten-Id raus</param>
        public SetCurrentTenantInterceptor(IServiceProvider services,
            Func<DbContext, string, int> resolveTenantId)
        {
            this.services = services;
            this.resolveTenantId = resolveTenantId
                                   ?? throw new ArgumentNullException(nameof(resolveTenantId));
        }

        /// <summary>
        /// Die Nachschlage-Funktion einer mitgelieferten Auspraegung. Oeffentlich, weil sie auch ohne den
        /// Interceptor brauchbar ist (etwa in einem eigenen Interceptor derselben Familie).
        /// </summary>
        public static Func<DbContext, string, int> LookupFor(SetTenantType tenantType)
            => tenantType switch
            {
                SetTenantType.Tenant => TenantIdLookup.For<Tenant>(),
                SetTenantType.BinderTenant => TenantIdLookup.For<BinderTenant>(),
                SetTenantType.HierarchyTenant => TenantIdLookup.For<HierarchyTenant>(),
                SetTenantType.HierarchyBinderTenant => TenantIdLookup.For<HierarchyBinderTenant>(),
                // Kein stiller Rueckfall auf "irgendwas": ein unbekannter Wert hiesse, dass die
                // Konfiguration eine Auspraegung nennt, die es nicht gibt - und die Fremdschluessel
                // bekaemen still eine falsche Id.
                _ => throw new ArgumentOutOfRangeException(nameof(tenantType), tenantType,
                    $"Unknown tenant flavour '{tenantType}'.")
            };

        /// <inheritdoc/>
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = new CancellationToken())
        {
            return ValueTask.FromResult(SavingChanges(eventData, result));
        }

        /// <inheritdoc/>
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (eventData.Context != null)
            {
                var scopeProvider = services.GetService<IPermissionScope>();
                if (scopeProvider != null)
                {
                    if (!string.IsNullOrEmpty(scopeProvider.PermissionPrefix))
                    {
                        AssignTenant(eventData.Context, scopeProvider.PermissionPrefix);
                    }
                    else
                    {
                        LogEnvironment.LogEvent(
                            "Current Context is not bound to a specific tenant. No action is performed.",
                            LogSeverity.Report);
                    }
                }
                else
                {
                    LogEnvironment.LogEvent($"No Service of Type {typeof(IPermissionScope)} was found.",
                        LogSeverity.Error);
                }
            }

            return base.SavingChanges(eventData, result);
        }

        /// <summary>
        /// Traegt den Mandanten in die ausgezeichneten Eigenschaften der neu angelegten Datensaetze ein.
        /// </summary>
        /// <remarks>
        /// Die Mandanten-Id wird <b>erst beim ersten Bedarf</b> nachgeschlagen. Der weitaus haeufigste
        /// Speichervorgang beruehrt gar keine ausgezeichnete Eigenschaft - eine Abfrage je SaveChanges
        /// waere dort reine Last. Als Nebenwirkung faellt ein nicht aufloesbarer Mandantenname nur dann
        /// auf, wenn er tatsaechlich gebraucht wird.
        /// </remarks>
        private void AssignTenant(DbContext context, string currentScopeName)
        {
            int? resolved = null;
            int ScopeAsInt() => resolved ??= resolveTenantId(context, currentScopeName);
            string scopeAsString = currentScopeName;
            Guid ScopeAsGuid() => Guid.Parse(currentScopeName);

            var entries = context.ChangeTracker.Entries().ToList();
            foreach (var entry in entries)
            {
                if (entry.State != EntityState.Added)
                {
                    continue;
                }

                foreach (var m in entry.Members
                             .Where(m => m.Metadata.PropertyInfo != null
                                         && Attribute.IsDefined(m.Metadata.PropertyInfo,
                                             typeof(AssignTenantAttribute)))
                             .Select(f => new { f.Metadata.PropertyInfo }))
                {
                    if (m.PropertyInfo.PropertyType == typeof(int) &&
                        m.PropertyInfo.GetValue(entry.Entity) is 0 ||
                        m.PropertyInfo.PropertyType == typeof(int?) &&
                        m.PropertyInfo.GetValue(entry.Entity) is null)
                    {
                        m.PropertyInfo.SetValue(entry.Entity, ScopeAsInt());
                    }
                    else if (m.PropertyInfo.PropertyType == typeof(int?) &&
                             m.PropertyInfo.GetValue(entry.Entity) is <= 0)
                    {
                        m.PropertyInfo.SetValue(entry.Entity, null);
                    }
                    else if (m.PropertyInfo.PropertyType == typeof(string) &&
                             string.IsNullOrEmpty((string)m.PropertyInfo.GetValue(entry.Entity)))
                    {
                        m.PropertyInfo.SetValue(entry.Entity, scopeAsString);
                    }
                    else if (m.PropertyInfo.PropertyType == typeof(string) &&
                             m.PropertyInfo.GetValue(entry.Entity) is "null")
                    {
                        m.PropertyInfo.SetValue(entry.Entity, null);
                    }
                    else if (m.PropertyInfo.PropertyType == typeof(Guid) &&
                             m.PropertyInfo.GetValue(entry.Entity) is Guid g && g == Guid.Empty ||
                             m.PropertyInfo.PropertyType == typeof(Guid?) &&
                             m.PropertyInfo.GetValue(entry.Entity) is null)
                    {
                        m.PropertyInfo.SetValue(entry.Entity, ScopeAsGuid());
                    }
                    else if (m.PropertyInfo.PropertyType == typeof(Guid?) &&
                             m.PropertyInfo.GetValue(entry.Entity) is Guid gnu && gnu == Guid.Empty)
                    {
                        m.PropertyInfo.SetValue(entry.Entity, null);
                    }
                }
            }
        }
    }
}
