using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Config;

namespace ITVComponents.InterProcessCommunication.Grpc
{
    public class HubSettings
    {
        public bool TrustAllCertificates { get;set; }

        public List<string> TrustedCertificates { get; set; } = new List<string>();

        public List<HubUser> HubUsers { get; set; }= new List<HubUser>();

        public List<HubRole> HubRoles{get;set;} = new List<HubRole>();

        public List<string> KnownHubPermissions { get; set; }
    }
}
