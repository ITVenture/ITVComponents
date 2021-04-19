using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    [Index(nameof(DiagnosticsQueryName), IsUnique = true, Name = "IX_DiagnosticsQueryUniqueness")]
    public class DiagnosticsQuery
    {
        public DiagnosticsQuery()
        {
        }

        [Key] public int DiagnosticsQueryId { get; set; }

        [Required, MaxLength(128)] public string DiagnosticsQueryName { get; set; }

        [Required, MaxLength(128)] public string DbContext { get; set; }

        public bool AutoReturn { get; set; }

        public string QueryText { get; set; }

        public int PermissionId { get; set; }

        [ForeignKey(nameof(PermissionId))] public virtual Permission Permission { get; set; }

        public virtual ICollection<DiagnosticsQueryParameter> Parameters { get;set; } = new List<DiagnosticsQueryParameter>();

        public virtual ICollection<TenantDiagnosticsQuery> Tenants { get; set; } = new List<TenantDiagnosticsQuery>();
    }
}
