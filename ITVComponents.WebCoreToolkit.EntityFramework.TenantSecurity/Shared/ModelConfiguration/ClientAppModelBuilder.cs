using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ModelConfiguration
{
    /// <summary>
    /// Die Fluent-Konfiguration der ClientApp-Familie - alles, was Data-Annotations nicht koennen.
    /// </summary>
    /// <remarks>
    /// Die Familie kam bis PRE239 vollstaendig mit Attributen aus. Das geht seit dem Umbau nicht mehr:
    /// ein Index mit Filter und eine Beziehung ohne Navigation lassen sich nicht als Attribut ausdruecken.
    /// Aufgerufen wird das aus dem <c>OnModelCreating</c> jedes konkreten Kontexts, mit dessen konkreten
    /// Typen.
    /// </remarks>
    public static class ClientAppModelBuilder
    {
        /// <summary>
        /// Konfiguriert die ClientApp-Familie fuer eine konkrete Modell-Auspraegung.
        /// </summary>
        /// <typeparam name="TClientAppTemplate">die Template-Auspraegung</typeparam>
        /// <typeparam name="TAppPermissionSet">die Rechtebuendel-Auspraegung</typeparam>
        /// <typeparam name="TClientApp">die ClientApp-Auspraegung</typeparam>
        /// <param name="modelBuilder">der Modellbauer des Kontexts</param>
        public static void ConfigureClientApps<TClientAppTemplate, TAppPermissionSet, TClientApp>(
            this ModelBuilder modelBuilder)
            where TClientAppTemplate : class
            where TAppPermissionSet : class
            where TClientApp : class
        {
            // Die Buendel gehoeren dem Template. Von der Template-Seite her erklaert, weil
            // AppPermissionSet den Template-Typ nicht in seiner Parameterliste fuehrt - ihn aufzunehmen
            // zoege eine Lawine durch jede Entitaet, die TAppPermissionSet fuehrt.
            modelBuilder.Entity<TClientAppTemplate>()
                .HasMany<TAppPermissionSet>("PermissionSets")
                .WithOne()
                .HasForeignKey("ClientAppTemplateId")
                .OnDelete(DeleteBehavior.Cascade);

            // Die App haengt an ihrem Template. Ohne Navigation auf beiden Seiten - deshalb als einzige
            // Beziehung dieser Familie ganz per Fluent-API. Restrict, nicht Cascade: ein Template zu
            // loeschen, an dem noch Anwendungen haengen, ist ein Fehler und kein Aufraeumen.
            modelBuilder.Entity<TClientApp>()
                .HasOne<TClientAppTemplate>()
                .WithMany()
                .HasForeignKey("ClientAppTemplateId")
                .OnDelete(DeleteBehavior.Restrict);

            // UQ_TUserPerApp bleibt bewusst als Attribut an ClientAppAccess und OHNE Filter: beide
            // Datenbanken tun von sich aus das Richtige (siehe den Kommentar dort). Wer hier einen
            // HasFilter-Aufruf ergaenzt, macht es kaputt.
        }
    }
}
