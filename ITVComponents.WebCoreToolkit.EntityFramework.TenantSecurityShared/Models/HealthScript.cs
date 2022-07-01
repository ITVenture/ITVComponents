using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(HealthScriptName), IsUnique = true, Name="UQ_NamedHealthScript")]
    public class HealthScript
    {
        [Key]
        public int HealthScriptId { get; set; }

        [MaxLength(128)]
        public string HealthScriptName { get; set; }

        public string Script { get; set; }
    }
}
