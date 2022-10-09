using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration.Models
{
    [Index(nameof(TenantId), IsUnique = false, Name = "IX_PlugInTParamTenant")]
    public class DatabasePluginTypeParam
    {
        [Key]
        public int TypeParamId { get; set; }

        public int PluginId { get; set; }

        [MaxLength(200)]
        public string GenericTypeName { get; set; }

        [MaxLength(2048)]
        public string TypeExpression { get; set; }

        public string? TenantId { get; set; }


        [ForeignKey(nameof(PluginId))]
        public virtual DatabasePlugin Plugin { get; set; }
    }
}
