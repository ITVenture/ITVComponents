using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(SequenceName), nameof(TenantId), IsUnique=true, Name="UQ_SequenceName")]
    public class Sequence
    {
        [Key]
        public int SequenceId { get; set; }
        public int TenantId { get; set; }

        [MaxLength(300), Required]
        public string SequenceName { get; set; }

        public int MinValue { get; set; } = -1;

        public int MaxValue { get; set; } = int.MaxValue;

        public bool Cycle { get; set; } = false;

        public int StepSize { get; set; } = 1;

        public int CurrentValue { get; set; } = -1;

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }
    }
}
