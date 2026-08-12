using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Extensions
{
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Maps the help-system entities (<see cref="HelpTopic"/>, <see cref="HelpTopicContent"/>,
        /// <see cref="HelpResource"/>, <see cref="HelpResourceFile"/>, <see cref="HelpResourceFolder"/>). Call from the consuming context's
        /// <c>OnModelCreating</c>. Keys/indexes come from data annotations; this configures the self-referencing
        /// tree and delete behaviour. The help system is global, so no tenant global filter is applied.
        /// </summary>
        public static ModelBuilder ConfigureHelpSystemModel(this ModelBuilder modelBuilder)
        {
            // Self-referencing tree: block deleting a topic while it still has children (Restrict) so a subtree
            // can't vanish and SQL Server never sees a multiple-cascade-path on the self relation.
            modelBuilder.Entity<HelpTopic>()
                .HasOne(t => t.Parent).WithMany(t => t.Children)
                .HasForeignKey(t => t.ParentId).OnDelete(DeleteBehavior.Restrict);

            // Localized content/files are owned by their topic/resource -> cascade on delete.
            modelBuilder.Entity<HelpTopicContent>()
                .HasOne(c => c.Topic).WithMany(t => t.Contents)
                .HasForeignKey(c => c.HelpTopicId).OnDelete(DeleteBehavior.Cascade);

            // Ordnerbaum: wie beim Topic-Baum Restrict - ein Ordner mit Inhalt verschwindet nicht
            // versehentlich, und SQL Server sieht auf der Selbstbeziehung keinen mehrfachen Kaskadenpfad.
            modelBuilder.Entity<HelpResourceFolder>()
                .HasOne(f => f.Parent).WithMany(f => f.Children)
                .HasForeignKey(f => f.ParentId).OnDelete(DeleteBehavior.Restrict);

            // Der Ordner BESITZT die Ressource nicht - er ordnet sie nur ein. Restrict statt Cascade:
            // sonst nimmt ein geloeschter Ordner Ressourcen mit, deren Dateien noch referenziert werden.
            modelBuilder.Entity<HelpResource>()
                .HasOne(r => r.Folder).WithMany(f => f.Resources)
                .HasForeignKey(r => r.FolderId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<HelpResourceFile>()
                .HasOne(f => f.Resource).WithMany(r => r.Files)
                .HasForeignKey(f => f.HelpResourceId).OnDelete(DeleteBehavior.Cascade);

            return modelBuilder;
        }
    }
}
