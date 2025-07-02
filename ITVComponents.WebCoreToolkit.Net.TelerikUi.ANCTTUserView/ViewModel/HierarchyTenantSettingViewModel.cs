using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTreeTenantSecurityUserView.ViewModel
{
    public class HierarchyTenantSettingViewModel:TenantSettingViewModel
    {
        public bool Inheritable { get; set; } = true;
    }
}
