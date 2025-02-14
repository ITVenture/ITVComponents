using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Services.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Options
{
    public class IdentityUiOptions
    {
        public List<ManagementNavPageDefinition> ManagementPages { get; set; } = new();

        public bool UseDefaultPageSet { get; set; }
        public bool UseExternalLogins { get; set; }
        public bool UseDefaultIdentityNavigator { get; set; }
        public bool UseDefaultMailSender { get; set; }
        public bool RegisterPageHandlers { get; set; }
        public bool UseDefaultIdentityUserGuard { get; set; }
        public string IdentityUserType { get; set; }

        public bool UserNameIsEmail { get; set; } = true;
        public bool UseLocalAccounts { get; set; } = true;
    }
}
