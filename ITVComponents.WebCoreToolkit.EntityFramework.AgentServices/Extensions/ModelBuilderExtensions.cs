using ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Extensions
{
    /// <summary>
    /// Bildet die Tabelle der Agent-Dienste ab.
    /// </summary>
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Bildet <see cref="ClientAppServiceBinding"/> ab — aus <c>OnModelCreating</c> aufzurufen, damit die
        /// Migrationen dasselbe Modell sehen wie die Laufzeit.
        /// </summary>
        /// <typeparam name="TTenant">der Mandant, wie der Host ihn abgebildet hat</typeparam>
        /// <typeparam name="TClientApp">die ClientApp, wie der Host sie abgebildet hat</typeparam>
        /// <remarks>
        /// <para>
        /// Zwei echte Fremdschlüssel ohne Navigation. Gelöscht wird nur über die ClientApp
        /// (<c>Cascade</c>): eine Kasse, die es nicht mehr gibt, hat keine Rollen mehr. Der Mandant ist
        /// <c>NoAction</c> — sonst gäbe es zwei Kaskadenpfade (Mandant → ClientApp → Bindung und
        /// Mandant → Bindung), und SQL Server lehnt die Tabelle ab.
        /// </para>
        /// <para>
        /// Eindeutig je <c>(ClientAppId, Slot)</c>: wer zwei gleiche Geräte braucht, deklariert zwei Rollen.
        /// </para>
        /// </remarks>
        public static ModelBuilder ConfigureAgentServices<TTenant, TClientApp>(this ModelBuilder modelBuilder)
            where TTenant : Tenant
            where TClientApp : class
        {
            modelBuilder.Entity<ClientAppServiceBinding>(b =>
            {
                b.ToTable("ClientAppServiceBindings");
                b.HasIndex(n => new { n.ClientAppId, n.Slot }).IsUnique()
                    .HasDatabaseName("UQ_ClientAppServiceBinding_Slot");
                b.HasIndex(n => n.TenantId).HasDatabaseName("IX_ClientAppServiceBinding_Tenant");
                b.HasOne<TClientApp>().WithMany().HasForeignKey(n => n.ClientAppId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne<TTenant>().WithMany().HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.NoAction);
            });

            return modelBuilder;
        }
    }
}
