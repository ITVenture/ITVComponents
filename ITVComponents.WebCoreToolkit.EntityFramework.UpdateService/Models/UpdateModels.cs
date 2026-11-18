using System;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models
{
    /// <summary>
    /// Something an update service keeps up to date: the application itself (kind <c>base</c>) or a module of it
    /// (<c>module</c>). Platform-wide: every shop runs the same builds.
    /// </summary>
    public class UpdateProduct
    {
        [Key]
        public int UpdateProductId { get; set; }

        /// <summary>The id in the feed (<c>MiniStore.PosAgent</c>, <c>MiniStore.Printer.EpsonTM</c>). Never rename it: the update services know it.</summary>
        [Required, MaxLength(128)]
        public string ProductKey { get; set; } = string.Empty;

        /// <summary><c>base</c> or <c>module</c>.</summary>
        [Required, MaxLength(16)]
        public string Kind { get; set; } = string.Empty;

        /// <summary>For a module: the application it extends (its <see cref="ProductKey"/>); the module appears in that feed.</summary>
        [MaxLength(128)]
        public string? BaseProductKey { get; set; }

        [Required, MaxLength(256)]
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// One release of a product in one channel.
    /// </summary>
    public class UpdateRelease
    {
        [Key]
        public int UpdateReleaseId { get; set; }

        public int UpdateProductId { get; set; }

        [Required, MaxLength(32)]
        public string Channel { get; set; } = "stable";

        [Required, MaxLength(32)]
        public string Version { get; set; } = string.Empty;

        /// <summary>When it was published; the longest deferral counts from here.</summary>
        public DateTime PublishedUtc { get; set; }

        /// <summary>Install right away (short deadline for the application).</summary>
        public bool Urgent { get; set; }

        /// <summary>Who runs an older version gets this one as urgent.</summary>
        [MaxLength(32)]
        public string? MinimumVersion { get; set; }

        /// <summary>For a module: the versions of the application it fits (NuGet range).</summary>
        [MaxLength(64)]
        public string? Requires { get; set; }

        /// <summary>Withdrawn: no longer offered. Whoever installed it keeps it - there is no downgrade.</summary>
        public bool Withdrawn { get; set; }

        [Required, MaxLength(64)]
        public string Sha256 { get; set; } = string.Empty;

        public long Size { get; set; }

        /// <summary>Where the package lies in the package store.</summary>
        [Required, MaxLength(128)]
        public string StorageKey { get; set; } = string.Empty;

        /// <summary>The file name of the package as uploaded (<c>MiniStore.PosAgent-1.4.2.msi</c>).</summary>
        [Required, MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? UploadedBy { get; set; }
    }

    /// <summary>
    /// One block of a package kept in the database. A package is written and read block by block - never as a whole.
    /// </summary>
    public class UpdatePackageChunk
    {
        [Required, MaxLength(128)]
        public string StorageKey { get; set; } = string.Empty;

        public int ChunkIndex { get; set; }

        [Required]
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// A module every client app of a template gets (platform-wide, like the template).
    /// </summary>
    public class ClientAppTemplateModuleAssignment
    {
        [Key]
        public int ClientAppTemplateModuleAssignmentId { get; set; }

        public int ClientAppTemplateId { get; set; }

        [Required, MaxLength(128)]
        public string ModuleProductKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// A module one client app gets in addition to those of its template.
    /// </summary>
    public class ClientAppModuleAssignment
    {
        [Key]
        public int ClientAppModuleAssignmentId { get; set; }

        /// <summary>The tenant; must be the one of the client app - checked when writing.</summary>
        public int TenantId { get; set; }

        public int ClientAppId { get; set; }

        [Required, MaxLength(128)]
        public string ModuleProductKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// The update state a client app reported last (fetched from its agent).
    /// </summary>
    public class ClientAppUpdateState
    {
        [Key]
        public int ClientAppId { get; set; }

        public int TenantId { get; set; }

        [MaxLength(32)]
        public string? InstalledVersion { get; set; }

        [MaxLength(32)]
        public string? Phase { get; set; }

        [MaxLength(32)]
        public string? AvailableVersion { get; set; }

        [MaxLength(512)]
        public string? BlockedBy { get; set; }

        [MaxLength(1024)]
        public string? Error { get; set; }

        public DateTime? DeferredUntilUtc { get; set; }

        /// <summary>The state of the modules as reported (JSON).</summary>
        public string? ModulesJson { get; set; }

        public DateTime ReportedUtc { get; set; }
    }
}
