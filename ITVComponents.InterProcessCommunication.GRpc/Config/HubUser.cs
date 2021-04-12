using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.InterProcessCommunication.Grpc.Config
{
    [Serializable]
    public class HubUser
    {
        public string UserName { get; set; }

        public string AuthenticationType { get; set; }

        public List<CustomUserProperty> CustomInfo { get; set; } = new List<CustomUserProperty>();

        public List<string> Roles { get; set; } = new List<string>();
    }
}
