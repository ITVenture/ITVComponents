using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(Identifier), IsUnique=true)]
    public class Localization
    {
        public int LocalizationId { get; set; }

        [MaxLength(1024)]
        public string Identifier { get; set; }

        public virtual ICollection<LocalizationCulture> Cultures { get; set; } = new List<LocalizationCulture>();
    }
}
