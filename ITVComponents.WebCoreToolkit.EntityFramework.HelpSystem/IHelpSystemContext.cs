using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem
{
    /// <summary>
    /// Contract a consuming <see cref="DbContext"/> implements to host the help-system tables. Standalone —
    /// does NOT require a tenant-security context (the help system is global). Call
    /// <c>modelBuilder.ConfigureHelpSystemModel()</c> from <c>OnModelCreating</c> to map the entities.
    /// </summary>
    public interface IHelpSystemContext
    {
        DbSet<HelpTopic> HelpTopics { get; set; }

        DbSet<HelpTopicContent> HelpTopicContents { get; set; }

        DbSet<HelpResource> HelpResources { get; set; }

        DbSet<HelpResourceFile> HelpResourceFiles { get; set; }

        /// <summary>Backing table for the built-in reference resource store. Hosts using their own
        /// <c>IHelpResourceStore</c> still map it (harmless empty table) so the model stays uniform.</summary>
        DbSet<HelpResourceBlob> HelpResourceBlobs { get; set; }
    }
}
