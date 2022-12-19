using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
{
    public class NotificationModel
    {
        public string UserName { get; set; }

        public string RoleName { get; set; }

        public string TenantName { get; set; }

        public bool Broadcast { get; set; }

        public string Url { get; set; }

        public string Topic { get; set; }

        public Dictionary<string,object> Data { get; set; }
        public string[] UsersWithPermissions { get; set; }
    }
}
