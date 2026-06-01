using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.Resources;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel;
using ITVComponents.WebCoreToolkit.Security;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TextsAndMessagesHelper = ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Resources.TextsAndMessagesHelper;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Areas.Connectivity.Controllers
{

    [Authorize("HasPermission(Services.Connections.View,Services.Connections.Write),HasFeature(ITVAdminViews)"), Area("Connectivity"), ConstructedGenericControllerConvention]
    public class ExternalServiceController<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : Controller 
        where TTenant : Tenant 
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter> 
        where TWebPluginConstant : WebPluginConstant<TTenant> 
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter> 
        where TSequence : Sequence<TTenant> 
        where TTenantSetting : TenantSetting<TTenant> 
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>, new()
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    {
        private readonly IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> db;
        private readonly ISecurityRepository secRepo;
        private readonly IOAuthHttpClientFactory clientFactory;

        public ExternalServiceController(
            IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence,
                TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState,
                TExternalOAuthServiceTenantLogin, TTrustConfig> db, ISecurityRepository secRepo, IOAuthHttpClientFactory clientFactory)
        {
            this.db= db;
            this.secRepo = secRepo;
            this.clientFactory = clientFactory;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult OAuthConnection()
        {
            ViewData["tenantId"] = db.CurrentTenantId;
            return View();
        }

        public IActionResult ServiceDetailInfoTable(int oauthServiceId)
        {
            ViewData["OAuthServiceId"] = oauthServiceId;
            var service = db.ExternalOAuthServices.FirstOrDefault(n => n.OAuthServiceId == oauthServiceId).ToServiceDefinition<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>(true);
            return PartialView(service);
        }

        public IActionResult ServiceDetails(int oauthServiceId)
        {
            ViewData["OAuthServiceId"] = oauthServiceId;
            var isConnected =
                db.ExternalOAuthServiceTenantLogins.Any(n => n.OAuthServiceId == oauthServiceId && !n.Revoked);
            ViewData["IsConnected"] = isConnected;
            return PartialView();
        }

        [HttpGet]
        public IActionResult TestForm(int oauthServiceId)
        {
            var httpVerbs = EnumHelper.DescribeEnum<HttpVerbs>().Select(n => new ForeignKeyData<int>
            {
                FullRecord = new Dictionary<string, object>(),
                Key = n.Value,
                Label = n.Name
            }).ToArray();
            ViewData["httpVerbs"] = httpVerbs;
            var model = new ExternalServiceTestDataViewModel
            {
                ActionBodyContentType = "application/json",
                OAuthServiceId = oauthServiceId,
                ActionTypeId = (int)HttpVerbs.Get
            };

            return PartialView(model);
        }

        public IActionResult ServicesTable(int tenantId)
        {
            ViewData["TenantId"] = tenantId;
            return PartialView();
        }

        [HttpPost]
        public async Task<IActionResult> PerformServiceTest([FromForm]ExternalServiceTestDataViewModel requestModelData)
        {
            var service = db.ExternalOAuthServices.FirstOrDefault(n => n.OAuthServiceId == requestModelData.OAuthServiceId).ToServiceDefinition<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>(true);
            var serviceClient = clientFactory.Create(service.GlobalUniqueConnectionName);
            var method = (HttpVerbs)requestModelData.ActionTypeId;
            var requestMessage = new HttpRequestMessage(HttpMethod.Parse(method.ToString()), requestModelData.TargetUrl);
            if (!string.IsNullOrEmpty(requestModelData.HttpActionBody))
            {
                requestMessage.Content =
                    new StringContent(requestModelData.HttpActionBody, new UTF8Encoding(), requestModelData.ActionBodyContentType);
            }

            if (!string.IsNullOrEmpty(requestModelData.CustomHeaders))
            {
                var hd = requestModelData.CustomHeaders.Split(";",
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (var h in hd)
                {
                    var fco = h.IndexOf(":");
                    var headerName = h.Substring(0, fco).Trim();
                    var headerValue = h.Substring(fco + 1).Trim();
                    requestMessage.Headers.Add(headerName, headerValue);
                }
            }

            var result = await serviceClient.SendAsync(requestMessage);
            var resultContent = await result.Content.ReadAsStringAsync();
            return Json(new { result.StatusCode, Content = resultContent });
        }

        [HttpPost]
        public IActionResult ReadForConnect([DataSourceRequest] DataSourceRequest request)
        {
            request = request.RemapRequestMembers(c =>
            {
                string retVal = null;
                switch (c)
                {
                    case nameof(ExternalOAuthServiceViewModel.UniqueConnectionName):
                        retVal = "Connection.UniqueConnectionName";
                        break;
                    case nameof(ExternalOAuthServiceViewModel.Global):
                        retVal = "Connection.Global";
                        break;

                }

                return retVal;
            });
            
            return Json((from s in db.ExternalOAuthServices join c in db.ExternalOAuthServiceTenantLogins on new { s.OAuthServiceId, TenantId=db.CurrentTenantId.Value } equals new { c.OAuthServiceId, TenantId=c.TenantId }
                into lj
                from j in lj.DefaultIfEmpty()
                         where s.TenantId == db.CurrentTenantId || s.TenantId == null
                         select new {Connection = s, Login=j}).ToDataSourceResult(request,
                n =>
                {
                    var tmp = n.Connection.ToViewModel<TExternalOAuthService, ExternalOAuthServiceViewModel>();
                    tmp.Editable = false;
                    tmp.ClientSecret = null;
                    tmp.IsConnected = n.Login != null || n.Connection.AuthenticationType != ExternalServiceAuthenticationType.OAuthAuthorizationFlow;
                    return tmp;
                }));
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request, int? tenantId)
        {
            var isAdmin = HttpContext.RequestServices.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });
            if (!isAdmin && tenantId == null)
            {
                tenantId = db.CurrentTenantId;
            }

            IQueryable<TExternalOAuthService> servicesRaw;
            if (!isAdmin)
            {
                servicesRaw = db.ExternalOAuthServices.Where(n => n.TenantId == tenantId);
            }
            else
            {
                servicesRaw = db.ExternalOAuthServices.Where(n => n.TenantId == tenantId || n.TenantId == null);
            }

            return Json(servicesRaw.ToDataSourceResult(request,
                n =>
                BuildViewModel(n,isAdmin,tenantId)));
        }

        [HttpPost]
        [Authorize("HasPermission(Services.Connections.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request,
            ExternalOAuthServiceViewModel viewModel, int? tenantId)
        {
            var isAdmin = HttpContext.RequestServices.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });
            var model = new TExternalOAuthService();
            if (!isAdmin)
            {
                viewModel.Global = false;
            }

            if (!viewModel.Global)
            {
                tenantId ??= db.CurrentTenantId;
            }

            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<ExternalOAuthServiceViewModel, TExternalOAuthService>(model, "", n => n.ElementType == null && n.Name != nameof(model.ClientSecret) && n.Name != nameof(model.CalculatedUniqueServiceName));
                if (tenantId != null && tenantId != 0)
                {
                    model.TenantId = tenantId;
                }

                EncryptSecret(model, viewModel.ClientSecret);
                db.ExternalOAuthServices.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { BuildViewModel(model, isAdmin, tenantId) }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Services.Connections.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request,
            ExternalOAuthServiceViewModel viewModel)
        {
            var isAdmin = HttpContext.RequestServices.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });
            //var model = new TExternalOAuthService();
            var model = db.ExternalOAuthServices.First(n => n.OAuthServiceId == viewModel.OAuthServiceId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<ExternalOAuthServiceViewModel, TExternalOAuthService>(model,"",n => n.ElementType == null && n.Name != nameof(model.ClientSecret) && n.Name != nameof(model.CalculatedUniqueServiceName));
                EncryptSecret(model, viewModel.ClientSecret);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { BuildViewModel(model, isAdmin , model.TenantId) }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Services.Connections.Write)")]
        public async Task<IActionResult> Delete([DataSourceRequest] DataSourceRequest request,
            ExternalOAuthServiceViewModel viewModel)
        {
            var isAdmin = HttpContext.RequestServices.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });
            //var model = new TExternalOAuthService();
            var model = db.ExternalOAuthServices.First(n => n.OAuthServiceId == viewModel.OAuthServiceId);
            db.ExternalOAuthServices.Remove(model);

            return Json(await new[] { BuildViewModel(model, isAdmin, model.TenantId) }
                .ToDataSourceResultAsync(request, ModelState));
        }

        private void EncryptSecret(TExternalOAuthService model, string secret)
        {
            if (model.Global && !string.IsNullOrEmpty(secret) && secret.StartsWith("encrypt:"))
            {
                model.ClientSecret = secret.Substring(8).Encrypt();
            }
            else if (!model.Global && !string.IsNullOrEmpty(secret) && secret.StartsWith("encrypt:"))
            {
                var t = db.Tenants.First(n => n.TenantId == model.TenantId);
                model.ClientSecret = secRepo.Encrypt(secret.Substring(8), t.TenantName);
            }
        }

        private ExternalOAuthServiceViewModel BuildViewModel(TExternalOAuthService n, bool isAdmin, int? tenantId)
        {
            {
                var tmp = n.ToViewModel<TExternalOAuthService, ExternalOAuthServiceViewModel>();
                tmp.Editable = (isAdmin && n.TenantId == null && tenantId == null) ||
                               (!isAdmin && n.TenantId != null);
                tmp.ClientSecret = string.Empty;//TextsAndMessagesHelper.IWCN_ES_UseEncryptPrefix;
                return tmp;
            }
        }
    }
}
