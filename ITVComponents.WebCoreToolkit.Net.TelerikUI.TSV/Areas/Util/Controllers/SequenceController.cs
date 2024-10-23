﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(Sequences.View,Sequences.Write),HasFeature(ITVAdminViews)"), Area("Util"), ConstructedGenericControllerConvention]
    public class SequenceController<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> : Controller where TTenant : Tenant where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter> where TWebPluginConstant : WebPluginConstant<TTenant> where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter> where TSequence : Sequence<TTenant>, new() where TTenantSetting : TenantSetting<TTenant> where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    {
        private readonly IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> db;

        public SequenceController(IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> db)
        {
            this.db = db;
            db.ShowAllTenants = true;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult SequenceTable(int tenantId)
        {
            return PartialView(tenantId);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request, int? tenantId)
        {
            if (tenantId == null)
            {
                db.ShowAllTenants = false;
            }

            return Json(db.Sequences.Where(n => n.TenantId == (tenantId??db.CurrentTenantId)).ToDataSourceResult(request,
                n => n.ToViewModel<TSequence, SequenceViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Sequences.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request, int? tenantId)
        {
            var model = new TSequence();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<SequenceViewModel, TSequence>(model);
                model.TenantId = tenantId ?? db.CurrentTenantId??-1;
                model.CurrentValue = model.MinValue - 1;
                db.Sequences.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TSequence, SequenceViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sequences.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request,
            SequenceViewModel viewModel)
        {
            var model = db.Sequences.First(n => n.SequenceId == viewModel.SequenceId);
            if (ModelState.IsValid)
            {
                db.Sequences.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sequences.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request,
            SequenceViewModel viewModel)
        {
            var model = db.Sequences.First(n => n.SequenceId == viewModel.SequenceId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<SequenceViewModel, TSequence>(model, "",
                    m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TSequence, SequenceViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }
    }
}
