using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.DbLessConfig.Models
{
    public class Role
    {
        public string RoleName { get; set; }

        public string[] Permissions{get; set; }
    }
}
