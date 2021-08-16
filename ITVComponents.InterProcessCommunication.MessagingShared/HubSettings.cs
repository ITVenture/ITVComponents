using System.Collections.Generic;
using ITVComponents.InterProcessCommunication.MessagingShared.Config;

namespace ITVComponents.InterProcessCommunication.MessagingShared
{
    public class HubSettings
    {
        public List<HubUser> HubUsers { get; set; }= new List<HubUser>();

        public List<HubRole> HubRoles{get;set;} = new List<HubRole>();

        public List<string> KnownHubPermissions { get; set; }
    }
}
