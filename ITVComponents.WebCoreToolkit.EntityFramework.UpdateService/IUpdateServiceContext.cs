using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService
{
    /// <summary>
    /// The tables of the update service on the web side. Opt-in: a host without update service does not implement it and
    /// gets no tables. Mapped with <c>modelBuilder.ConfigureUpdateService&lt;TTenant, TClientApp, TClientAppTemplate&gt;()</c>.
    /// </summary>
    /// <remarks>
    /// Products, releases, package blocks and template assignments are platform-wide; the assignment and the state of a
    /// client app belong to its tenant.
    /// </remarks>
    public interface IUpdateServiceContext
    {
        DbSet<UpdateProduct> UpdateProducts { get; set; }

        DbSet<UpdateRelease> UpdateReleases { get; set; }

        /// <summary>The package blocks of the database package store; empty when packages lie on a file volume.</summary>
        DbSet<UpdatePackageChunk> UpdatePackageChunks { get; set; }

        DbSet<ClientAppTemplateModuleAssignment> ClientAppTemplateModuleAssignments { get; set; }

        DbSet<ClientAppModuleAssignment> ClientAppModuleAssignments { get; set; }

        DbSet<ClientAppUpdateState> ClientAppUpdateStates { get; set; }
    }
}
