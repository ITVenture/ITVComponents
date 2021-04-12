using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    [Index(nameof(SettingsKey), nameof(TenantId), IsUnique=true, Name="UQ_SettingsKey")]
    public class TenantSetting
    {
        [Key]
        public int TenantSettingId { get; set; }

        public int TenantId { get; set; }

        [MaxLength(100),Required]
        public string SettingsKey { get;set; }

        public string SettingsValue { get; set; }

        public bool JsonSetting { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }
    }
}
