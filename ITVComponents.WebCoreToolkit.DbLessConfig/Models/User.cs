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

        public Dictionary<string,string> CustomInfo { get; set; } = new Dictionary<string, string>();

        public string[] Roles { get; set; }
        
        public string AuthenticationType { get; set; }
    }
}
