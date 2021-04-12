using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.InterProcessCommunication.Grpc.Security.Options
{
    
    [SettingName("IpcUserClaimRedirect")]
    public class UserClaimRedirect
    {
        public string UserNameClaim { get; set; }
        
        public string UserRoleClaim{get;set;}
    }
}
