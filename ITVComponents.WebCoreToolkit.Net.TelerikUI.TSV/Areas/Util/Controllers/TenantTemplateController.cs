using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(Sysadmin),HasFeature(ITVAdminViews)"), Area("Util"), ConstructedGenericControllerConvention]
    public class TenantTemplateController<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> : Controller 
        where TTenant : Tenant 
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    {
        public IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> db;
        private readonly ITenantTemplateHelper<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> templateHelper;

        public TenantTemplateController(IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> db, ITenantTemplateHelper<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> templateHelper)
        {
            this.db = db;
            this.templateHelper = templateHelper;
            db.ShowAllTenants = true;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult TenantTemplateActivation(int tenantId)
        {
            ViewData["tenantId"] = tenantId;
            return View();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.TenantTemplates.ToDataSourceResult(request, ModelState, n => n.ToViewModel<TenantTemplate, TenantTemplateViewModel>()));
        }

        [HttpPost]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
           var model = new TenantTemplate();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TenantTemplateViewModel, TenantTemplate>(model);
                db.TenantTemplates.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TenantTemplate, TenantTemplateViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, TenantTemplateViewModel viewModel)
        {

           var model = db.TenantTemplates.First(n => n.TenantTemplateId == viewModel.TenantTemplateId);
            if (ModelState.IsValid)
            {
                db.TenantTemplates.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, TenantTemplateViewModel viewModel)
        {
            var model = db.TenantTemplates.First(n => n.TenantTemplateId == viewModel.TenantTemplateId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TenantTemplateViewModel, TenantTemplate>(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TenantTemplate, TenantTemplateViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        public async Task<IActionResult> ApplyTemplate([FromBody] ApplyTenantDataViewModel data)
        {
            var retVal = new TenantActionResultViewModel { Message = "OK", Success = true };
            try
            {
                var tn = db.Tenants.First(n => n.TenantId == data.TenantId);
                var tm = db.TenantTemplates.First(n => n.TenantTemplateId == data.TemplateId);
                var template = JsonHelper.FromJsonString<TenantTemplateMarkup>(tm.Markup);
                templateHelper.ApplyTemplate(tn, template);
            }
            catch (Exception ex)
            {
                retVal.Success = false;
                retVal.Message = ex.Message;
            }

            return Json(retVal);
        }

        public async Task<IActionResult> CreateTemplate([FromBody] CreateTenantTemplateDataViewModel data)
        {
            var retVal = new TenantActionResultViewModel { Message = "OK", Success = true };
            try
            {
                var tn = db.Tenants.First(n => n.TenantId == data.TenantId);
                var tm = templateHelper.ExtractTemplate(tn);
                var template = JsonHelper.ToJson(tm);
                var tmp = new TenantTemplate
                {
                    Name = data.Name,
                    Markup = template
                };
                db.TenantTemplates.Add(tmp);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                retVal.Success = false;
                retVal.Message = ex.Message;
            }

            return Json(retVal);
        }
    }
}
