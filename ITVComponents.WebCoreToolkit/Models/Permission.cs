using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models
{
    public class Permission
    {
        [MaxLength(150)]
        [Required]
        public string PermissionName { get; set; }
    }
}
