using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Options
{
    public class TenantOptions<TTenant> where TTenant : Tenant
    {
        public bool UseHierarchy { get; set; } = false;
        
        public Expression<Func<TTenant, TenantViewModel>> SelectTenant { get; set; }
        
        public Action<TTenant, TenantViewModel> UpdateTenant { get; set; }

        /// <summary>
        /// Gibt einem neu angelegten Mandanten einen eigenen Schluessel (<c>TenantPassword</c>), falls <see cref="UpdateTenant"/>
        /// keinen gesetzt hat. Ohne eigenen Schluessel verschluesselt der Mandant mit dem anwendungsweiten Schluessel, den er
        /// mit allen anderen solchen Mandanten teilt. Beim Aendern wird der Schluessel nie angefasst.
        /// </summary>
        public bool GenerateTenantPassword { get; set; } = true;
        public Func<DbContext, ISecurityAccessProvider, IDisposable?> ConfigureTree { get; set; }
        public Func<DbContext, int[], int[]> AddDirectParent { get; set; }
    }
}
