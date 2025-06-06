using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options
{
    public class ServerCookieOptions
    {
        public int DefaultCookieValidDays { get; set; } = 1;

        public int CookieLengthThreshold { get; set; } = 2048;
    }
}
