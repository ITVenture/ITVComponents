using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTreeTenantSecurityUserView.ViewModel
{
    [ViewModelForEntity(typeof(HierarchyTenant))]
    public class HierarchyTenantViewModel:TenantViewModel
    {
        public int? ParentTenantId { get; set; }
    }
}
