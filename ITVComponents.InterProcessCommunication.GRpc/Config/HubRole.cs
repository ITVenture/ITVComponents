using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Config
{
    [Serializable]
    public class HubRole
    {
        public string RoleName { get; set; }

        public List<string> Permissions { get; set; } = new List<string>();
    }
}
