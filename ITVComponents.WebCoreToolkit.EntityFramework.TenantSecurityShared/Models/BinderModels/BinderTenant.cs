using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels
{
    [BinderEntity]
    public class BinderTenant
    {

        [Key]
        public int TenantId { get; set; }

        [Required, MaxLength(150)]
        public string TenantName { get; set; }

        [MaxLength(1024)]
        public string DisplayName { get; set; }
    }
}
