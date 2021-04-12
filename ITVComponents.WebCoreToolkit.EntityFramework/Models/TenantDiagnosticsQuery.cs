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
    [Index(nameof(TenantId), nameof(DiagnosticsQueryId), IsUnique=true, Name="IX_UniqueDiagnosticsTenantLink")]
    public class TenantDiagnosticsQuery
    {
        [Key]
        public int TenantDiagnosticsQueryId { get;set; }

        public int TenantId { get; set; }

        public int DiagnosticsQueryId { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }

        [ForeignKey(nameof(DiagnosticsQueryId))]
        public virtual DiagnosticsQuery DiagnosticsQuery { get; set; }
    }
}
