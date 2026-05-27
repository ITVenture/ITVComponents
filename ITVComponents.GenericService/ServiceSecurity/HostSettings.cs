using System.Collections.Generic;
using ITVComponents.GenericService.ServiceSecurity.Config;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.GenericService.ServiceSecurity
{
    public class HostSettings
    {
        public List<HostUser> HostUsers { get; set; }= new List<HostUser>();

        public List<HostRole> HostRoles{get;set;} = new List<HostRole>();

        public List<string> KnownHostPermissions { get; set; }

        public List<Feature> Features { get; set; } = new List<Feature>();
    }
}
