using System;
using System.Collections.Generic;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Config
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
