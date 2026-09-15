using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ModelConfiguration
{
    /// <summary>
    /// Die Fluent-Konfiguration der ClientApp-Familie - das, was Data-Annotations nicht koennen.
    /// </summary>
    /// <remarks>
    /// Seit beide Beziehungen zum Template eine echte Navigation haben, bleibt hier nur noch das
    /// Loeschverhalten: die Annotationen sagen, WAS zusammenhaengt, aber nicht, was beim Loeschen des
    /// Templates passieren soll.
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
            // Ein Template zu loeschen nimmt seine Buendel mit - sie gehoeren ihm und haben ohne es keinen
            // Sinn.
            modelBuilder.Entity<TClientAppTemplate>()
                .HasMany<TAppPermissionSet>("PermissionSets")
                .WithOne("ClientAppTemplate")
                .HasForeignKey("ClientAppTemplateId")
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, nicht Cascade: ein Template zu loeschen, an dem noch ANWENDUNGEN haengen, ist ein
            // Fehler und kein Aufraeumen. Die Buendel oben darf es mitnehmen, die Anwendungen nicht.
            modelBuilder.Entity<TClientApp>()
                .HasOne<TClientAppTemplate>("ClientAppTemplate")
                .WithMany()
                .HasForeignKey("ClientAppTemplateId")
                .OnDelete(DeleteBehavior.Restrict);

            // UQ_TUserPerApp bleibt bewusst als Attribut an ClientAppAccess und OHNE Filter: beide
            // Datenbanken tun von sich aus das Richtige (siehe den Kommentar dort). Wer hier einen
            // HasFilter-Aufruf ergaenzt, macht es kaputt.
        }
    }
}
