using System.ComponentModel.DataAnnotations;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration.Models
{
    public class DatabasePlugin
    {
        [Key]
        public virtual int PluginId { get; set; }

        [MaxLength(50)]
        public virtual string UniqueName { get; set; }

        [MaxLength(2048)]
        public virtual string Constructor { get; set; }

        public virtual int LoadOrder { get; set; }

        public virtual bool? Disabled { get; set; }

        [MaxLength(4096)]
        public virtual string DisabledReason { get; set; }
    }
}