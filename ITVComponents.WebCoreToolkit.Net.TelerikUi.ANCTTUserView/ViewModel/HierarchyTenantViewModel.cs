using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTreeTenantSecurityUserView.ViewModel
{
    public class HierarchyTenantViewModel:TenantViewModel
    {
        public int? ParentTenantId { get; set; }
    }
}
