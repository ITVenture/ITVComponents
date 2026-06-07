using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models
{
    public class UserMailTokenData
    {
        public string UserId { get; set; }
        public string Code { get; set; }
        public bool Success { get; set; }
    }
}
