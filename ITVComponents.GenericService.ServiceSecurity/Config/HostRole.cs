using System;
using System.Collections.Generic;

namespace ITVComponents.GenericService.ServiceSecurity.Config
{
    [Serializable]
    public class HostRole
    {
        public string RoleName { get; set; }

        public List<string> Permissions { get; set; } = new List<string>();
    }
}
