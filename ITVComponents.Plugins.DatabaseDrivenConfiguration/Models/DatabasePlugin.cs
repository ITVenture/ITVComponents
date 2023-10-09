using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration.Models
{
    [Index(nameof(TenantId), IsUnique = false, Name = "IX_PlugInTenant")]
    public class DatabasePlugin
    {
        [Key]
        public int PluginId { get; set; }

        [MaxLength(50)]
        public string UniqueName { get; set; }

        [MaxLength(2048)]
        public string Constructor { get; set; }

        public int LoadOrder { get; set; }

        public string? TenantId { get; set; }

        public PluginInitializationPhase? PluginInitializationPhase { get; set; }

        public bool? Disabled { get; set; }

        public string PreInitializationList { get; set; }

        public string PostInitializationList { get; set; }

        [MaxLength(4096)]
        public string DisabledReason { get; set; }
    }
}