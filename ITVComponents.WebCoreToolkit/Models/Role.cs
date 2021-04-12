using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Models
{
    public class Role
    {
        [MaxLength(150)]
        [Required]
        public string RoleName { get; set; }
    }
}
