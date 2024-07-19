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
    [Index(nameof(CultureId), nameof(LocalizationId), IsUnique=true)]
    public class LocalizationCulture
    {
        [Key]
        public int LocalizationCultureId  { get; set; }

        public int CultureId { get; set; }

        public int LocalizationId { get; set; }

        [ForeignKey(nameof(CultureId))]
        public virtual Culture Culture { get; set; }

        [ForeignKey(nameof(LocalizationId))]
        public virtual Localization Localization { get; set; }

        public virtual ICollection<LocalizationString> Strings { get; set; } = new List<LocalizationString>();
    }
}
