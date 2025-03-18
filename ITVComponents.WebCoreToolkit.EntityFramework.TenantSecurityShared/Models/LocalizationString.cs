using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(LocalizationCultureId), nameof(LocalizationKey), IsUnique=true)]
    public class LocalizationString
    {
        public int LocalizationStringId { get; set; }

        public int LocalizationCultureId { get; set; }

        [Required, MaxLength(256)]
        public string LocalizationKey { get; set; }

        [Required]
        public string LocalizationValue { get; set; }
    }
}
