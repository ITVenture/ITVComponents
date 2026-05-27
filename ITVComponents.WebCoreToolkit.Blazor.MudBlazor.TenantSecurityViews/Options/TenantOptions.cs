using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Options
{
    public class TenantOptions<TTenant> where TTenant : Tenant
    {
        public bool UseHierarchy { get; set; } = false;
        
        public Expression<Func<TTenant, TenantViewModel>> SelectTenant { get; set; }
        
        public Action<TTenant, TenantViewModel> UpdateTenant { get; set; }
        public Func<DbContext, ISecurityAccessProvider, IDisposable?> ConfigureTree { get; set; }
        public Func<DbContext, int[], int[]> AddDirectParent { get; set; }
    }
}
