using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models
{
    public class UserRegistrationInfo
    {
        public bool AllowRegister { get; set; }
        public bool ControllerLink { get; set; }
        public string Area { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
        public string Page { get; set; }
    }
}
