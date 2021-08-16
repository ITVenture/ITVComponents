using System;
using System.Collections.Generic;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Config
{
    [Serializable]
    public class HubRole
    {
        public string RoleName { get; set; }

        public List<string> Permissions { get; set; } = new List<string>();
    }
}
