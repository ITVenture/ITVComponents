using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(Name), IsUnique = true)]
    public class Culture
    {
        [Key]
        public int CultureId { get; set; }

        [Required, MaxLength(128)]
        public string Name { get; set; }
    }
}
