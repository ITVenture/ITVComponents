using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.DbLessConfig.Models
{
    public class User
    {
        public string UserName { get; set; }

        public List<CustomUserProperty> CustomInfo { get; set; } = new List<CustomUserProperty>();

        public string[] Roles { get; set; }
        
        public string AuthenticationType { get; set; }
    }
}
