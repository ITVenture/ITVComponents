using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared
{
    /// <summary>
    /// Die ClientApps und ihre Templates, ohne die Rechte-Kette.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Sicherheitskontext erbt diesen Vertrag; andere Kontext-Verträge (etwa der der Agent-Dienste) können
    /// ihn ebenfalls erben und bekommen so einen echten Fremdschlüssel auf die ClientApp, ohne die neunzehn
    /// Typparameter der Sicherheitsschicht mitzuschleppen.
    /// </para>
    /// <para>
    /// Bewusst nicht in <c>IBaseTenantContext</c>: dessen Typparameter würden sich sonst für jeden
    /// Konsumenten ändern, auch für die ohne ClientApps.
    /// </para>
    /// </remarks>
    public interface IClientAppContext<TClientApp, TClientAppTemplate>
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        /// <summary>Die Client-Anwendungen.</summary>
        DbSet<TClientApp> ClientApps { get; set; }

        /// <summary>Die Templates der Client-Anwendungen.</summary>
        DbSet<TClientAppTemplate> ClientAppTemplates { get; set; }
    }
}
