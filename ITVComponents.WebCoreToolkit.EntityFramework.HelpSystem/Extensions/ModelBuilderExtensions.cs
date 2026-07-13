using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Extensions
{
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Maps the help-system entities (<see cref="HelpTopic"/>, <see cref="HelpTopicContent"/>,
        /// <see cref="HelpResource"/>, <see cref="HelpResourceFile"/>). Call from the consuming context's
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

            modelBuilder.Entity<HelpResourceFile>()
                .HasOne(f => f.Resource).WithMany(r => r.Files)
                .HasForeignKey(f => f.HelpResourceId).OnDelete(DeleteBehavior.Cascade);

            return modelBuilder;
        }
    }
}
