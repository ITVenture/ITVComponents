using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(PlugInConstants.Write,PlugInConstants.View),HasFeature(ITVAdminViews)"), Area("Util"), ConstructedGenericControllerConvention]
    public class PlugInConstController<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> : Controller 
        where TTenant : Tenant 
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>, new()
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    {
        private readonly IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> db;
        private readonly IOptions<SecurityViewsOptions> options;
        private bool isSysAdmin;

        public PlugInConstController(IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> db, IServiceProvider services, IOptions<SecurityViewsOptions> options)
        {
            this.db = db;
            this.options = options;
            if (!services.VerifyUserPermissions(new[] { EntityFramework.TenantSecurityShared.Helpers.ToolkitPermission.Sysadmin}))
            {
                db.HideGlobals = true;
                isSysAdmin = false;
            }
            else
            {
                db.ShowAllTenants = true;
                isSysAdmin = true;
            }
        }

        public IActionResult Index()
        {
            int? tenantId = null;
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            
            ViewData["tenantId"] = tenantId;
            return View();
        }
        
        public IActionResult ConstTable(int tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId.Value;
            }

            return View(tenantId);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request,[FromQuery] int? tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            
            return Json(db.WebPluginConstants.Where(n => n.TenantId == tenantId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<TWebPluginConstant, WebPluginConstantViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(PlugInConstants.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request, [FromQuery]int? tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            var model = new TWebPluginConstant();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<WebPluginConstantViewModel, TWebPluginConstant>(model);
                model.TenantId = tenantId;
                db.WebPluginConstants.Add(model);
                if (model.Value.StartsWith("encrypt:"))
                {
                    if (tenantId == null || !options.Value.UseExplicitTenantPasswords)
                    {
                        model.Value = PasswordSecurity.Encrypt(model.Value.Substring(8));
                    }
                    else
                    {
                        var t = db.Tenants.First(n => n.TenantId == tenantId);
                        if (!string.IsNullOrEmpty(t.TenantPassword))
                        {
                            var pwd = Convert.FromBase64String(t.TenantPassword);
                            model.Value = AesEncryptor.Encrypt(model.Value.Substring(8), pwd);
                        }
                        else
                        {
                            model.Value = PasswordSecurity.Encrypt(model.Value.Substring(8));
                        }
                    }
                }

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TWebPluginConstant, WebPluginConstantViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(PlugInConstants.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, WebPluginConstantViewModel viewModel)
        {
            var tenantId = viewModel.TenantId;
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            
            var model = db.WebPluginConstants.First(n => n.TenantId == tenantId && n.WebPluginConstantId == viewModel.WebPluginConstantId);
            if (ModelState.IsValid)
            {
                db.WebPluginConstants.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(PlugInConstants.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, WebPluginConstantViewModel viewModel)
        {
            var tenantId = viewModel.TenantId;
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            
            var model = db.WebPluginConstants.First(n => n.TenantId == tenantId && n.WebPluginConstantId == viewModel.WebPluginConstantId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<WebPluginConstantViewModel, TWebPluginConstant>(model, "", m => { return m.ElementType == null; });
                if (model.Value.StartsWith("encrypt:"))
                {
                    if (tenantId == null || !options.Value.UseExplicitTenantPasswords)
                    {
                        model.Value = PasswordSecurity.Encrypt(model.Value.Substring(8));
                    }
                    else
                    {
                        var t = db.Tenants.First(n => n.TenantId == tenantId);
                        if (!string.IsNullOrEmpty(t.TenantPassword))
                        {
                            var pwd = Convert.FromBase64String(t.TenantPassword);
                            model.Value = AesEncryptor.Encrypt(model.Value.Substring(8), pwd);
                        }
                        else
                        {
                            model.Value = PasswordSecurity.Encrypt(model.Value.Substring(8));
                        }
                    }
                }
                model.TenantId = tenantId;
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TWebPluginConstant, WebPluginConstantViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
