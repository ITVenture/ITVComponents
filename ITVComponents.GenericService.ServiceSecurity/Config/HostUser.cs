using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.GenericService.ServiceSecurity.Config
{
    [Serializable]
    public class HostUser
    {
        public string UserName { get; set; }

        public string AuthenticationType { get; set; }

        public List<CustomUserProperty> CustomInfo { get; set; } = new List<CustomUserProperty>();

        public List<string> Roles { get; set; } = new List<string>();
    }
}
