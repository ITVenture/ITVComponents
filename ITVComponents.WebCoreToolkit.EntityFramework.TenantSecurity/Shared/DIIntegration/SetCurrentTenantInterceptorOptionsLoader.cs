using System;
using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Interceptors;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DIIntegration
{
    /// <summary>
    /// Haengt den <see cref="SetCurrentTenantInterceptor"/> an einen Kontext - fuer eine der
    /// mitgelieferten Mandanten-Auspraegungen (<see cref="SetTenantType"/>).
    /// </summary>
    public class SetCurrentTenantInterceptorOptionsLoader<TContext> : ContextOptionsLoader<TContext>
        where TContext : DbContext
    {
        private readonly IServiceProvider services;
        private readonly Func<DbContext, string, int> lookup;

        /// <summary>
        /// Erzeugt den Loader fuer eine der mitgelieferten Auspraegungen. <b>Der Weg aus der
        /// Konfiguration</b> - die Auspraegung steht dort als Name.
        /// </summary>
        public SetCurrentTenantInterceptorOptionsLoader(ContextOptionsLoader<TContext> parent,
            IServiceProvider services, SetTenantType tenantType)
            : this(parent, services, SetCurrentTenantInterceptor.LookupFor(tenantType))
        {
        }

        /// <summary>
        /// Erzeugt den Loader mit einer eigenen Nachschlage-Funktion - fuer eine Mandanten-Entitaet, die
        /// diese Bibliothek nicht kennt.
        /// </summary>
        protected SetCurrentTenantInterceptorOptionsLoader(ContextOptionsLoader<TContext> parent,
            IServiceProvider services, Func<DbContext, string, int> lookup) : base(parent)
        {
            this.services = services;
            this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        }

        /// <inheritdoc/>
        protected override void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> builder)
        {
            builder.AddInterceptors(new SetCurrentTenantInterceptor(services, lookup));
        }
    }

    /// <summary>
    /// Dasselbe fuer eine <b>beliebige</b> Mandanten-Entitaet: die Auspraegung steht als Typ-Argument,
    /// nicht als Aufzaehlungswert.
    /// </summary>
    /// <typeparam name="TContext">der Kontext</typeparam>
    /// <typeparam name="TTenant">die Mandanten-Entitaet dieses Kontexts</typeparam>
    /// <remarks>
    /// Fuer die vier mitgelieferten Auspraegungen ist der Weg ueber <see cref="SetTenantType"/> der
    /// bequemere (ein Name in der Konfiguration statt eines generischen Typs). Diese Fassung ist fuer
    /// alles darueber hinaus - sie kommt ohne jede Kenntnis der konkreten Entitaet in der Bibliothek
    /// aus.
    /// </remarks>
    public class SetCurrentTenantInterceptorOptionsLoader<TContext, TTenant>
        : SetCurrentTenantInterceptorOptionsLoader<TContext>
        where TContext : DbContext
        where TTenant : class, ITenantIdentity
    {
        /// <summary>Erzeugt den Loader fuer die als Typ-Argument genannte Mandanten-Entitaet.</summary>
        public SetCurrentTenantInterceptorOptionsLoader(ContextOptionsLoader<TContext> parent,
            IServiceProvider services)
            : base(parent, services, TenantIdLookup.For<TTenant>())
        {
        }
    }

    /// <summary>
    /// Welche Mandanten-Entitaet ein Kontext fuehrt. Die beiden Achsen sind unabhaengig: <b>flach oder
    /// hierarchisch</b> und <b>vollstaendiges Modell oder Binder</b>.
    /// </summary>
    public enum SetTenantType
    {
        /// <summary>Das vollstaendige, flache Mandanten-Modell (<c>Shared.Models.Tenant</c>).</summary>
        Tenant,

        /// <summary>Die schlanke, flache Binder-Fassung (<c>Shared.Models.BinderModels.BinderTenant</c>).</summary>
        BinderTenant,

        /// <summary>Das vollstaendige, hierarchische Modell (<c>TreeShared.Models.HierarchyTenant</c>).</summary>
        HierarchyTenant,

        /// <summary>
        /// Die schlanke, hierarchische Binder-Fassung
        /// (<c>TreeShared.Models.BinderModels.BinderTenant</c>).
        /// </summary>
        HierarchyBinderTenant
    }
}
