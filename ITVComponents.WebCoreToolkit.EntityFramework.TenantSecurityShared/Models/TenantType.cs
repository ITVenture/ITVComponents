using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(propertyName:nameof(TenantTypeName), IsUnique=true, Name = "UQ_TenantTypeName")]
    public class TenantType
    {
        [Key]
        public int TenantTypeId { get; set; }

        [MaxLength(512), Required]
        public string TenantTypeName { get; set; }

        [ExcludeFromDictionary]
        public string? TypeMetaData { get; set; }

        public int? TenantTemplateId { get; set; }

        [ForeignKey(nameof(TenantTemplateId))]
        public virtual TenantTemplate? TenantTemplate { get; set; }
    }
}
