using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.PermissionFlagging.Models
{
    public class PermissionFlag
    {
        public string Category { get; set; }

        public string[] PermissionGroups { get; init; }
    }
}
