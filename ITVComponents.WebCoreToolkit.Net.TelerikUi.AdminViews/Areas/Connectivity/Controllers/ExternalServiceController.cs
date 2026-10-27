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
                db.ExternalOAuthServiceTenantLogins.Any(n => n.OAuthServiceId == oauthServiceId && !n.Revoked)
                || db.ExternalOAuthServices.FirstOrDefault(n=> n.OAuthServiceId == oauthServiceId)?.AuthenticationType != ExternalServiceAuthenticationType.OAuthAuthorizationFlow;
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
                // Ein Mandanten-Admin legt nur fuer den EIGENEN Mandanten an - was die Anfrage als tenantId mitbringt,
                // zaehlt nicht.
                viewModel.Global = false;
                tenantId = db.CurrentTenantId;
            }

            if (viewModel.Global)
            {
                // Global und TenantId gehoeren zusammen: Global entscheidet den Schluessel des Geheimnisses, TenantId die
                // Sichtbarkeit. Beides gesetzt ergaebe einen Dienst, der mit dem Systemschluessel verschluesselt, aber
                // als Mandantendienst gelesen wird.
                tenantId = null;
            }
            else
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
            var model = FindEditableService(viewModel.OAuthServiceId, isAdmin);
            if (model == null)
            {
                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
            }

            if (ModelState.IsValid)
            {
                // Global wird hier nicht umgeschaltet: es entscheidet, mit welchem Schluessel das Geheimnis liegt, und ein
                // Umschalten ohne Neuverschluesselung machte es unlesbar. Wer es aendern will, legt den Dienst neu an.
                await this.TryUpdateModelAsync<ExternalOAuthServiceViewModel, TExternalOAuthService>(model,"",n => n.ElementType == null && n.Name != nameof(model.ClientSecret) && n.Name != nameof(model.CalculatedUniqueServiceName) && n.Name != nameof(model.Global) && n.Name != nameof(model.TenantId) && n.Name != nameof(model.OAuthServiceId));
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
            var model = FindEditableService(viewModel.OAuthServiceId, isAdmin);
            if (model == null)
            {
                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
            }

            db.ExternalOAuthServices.Remove(model);
            await db.SaveChangesAsync();

            return Json(await new[] { BuildViewModel(model, isAdmin, model.TenantId) }
                .ToDataSourceResultAsync(request, ModelState));
        }

        public IActionResult ParameterTable(int oauthServiceId)
        {
            ViewData["OAuthServiceId"] = oauthServiceId;
            var service = db.ExternalOAuthServices.FirstOrDefault(n => n.OAuthServiceId == oauthServiceId);
            ViewData["SupportsParameters"] = service != null && TokenRequestParameterRules.SupportsParameters(service.AuthenticationType);
            return PartialView();
        }

        [HttpPost]
        public IActionResult ReadParameters([DataSourceRequest] DataSourceRequest request, int oauthServiceId)
        {
            // the parameters have no tenant-filter of their own; they are only reachable through the (filtered) service
            var service = db.ExternalOAuthServices.FirstOrDefault(n => n.OAuthServiceId == oauthServiceId);
            var parameters = service?.TokenParameters ?? new List<ExternalOAuthServiceParameter>();
            return Json(parameters.ToDataSourceResult(request, n => ToParameterViewModel(n)));
        }

        [HttpPost]
        [Authorize("HasPermission(Services.Connections.Write)")]
        public async Task<IActionResult> CreateParameter([DataSourceRequest] DataSourceRequest request, [FromQuery] int oauthServiceId, ExternalOAuthServiceParameterViewModel viewModel)
        {
            var service = FindServiceForParameterEdit(oauthServiceId);
            ExternalOAuthServiceParameter model = null;
            if (service != null && ValidateParameter(service, viewModel) && ModelState.IsValid)
            {
                // Die Wertregel (geheim / Klartext) steht in ApplyParameterValue, fuer beide Wege gleich.
                model = new ExternalOAuthServiceParameter
                {
                    ParameterName = viewModel.ParameterName.Trim()
                };
                if (ApplyParameterValue(service, model, viewModel))
                {
                    service.TokenParameters.Add(model);
                    await db.SaveChangesAsync();
                }
                else
                {
                    model = null;
                }
            }

            return Json(await new[] { model != null ? ToParameterViewModel(model) : viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Services.Connections.Write)")]
        public async Task<IActionResult> UpdateParameter([DataSourceRequest] DataSourceRequest request, ExternalOAuthServiceParameterViewModel viewModel)
        {
            var service = FindServiceForParameterEdit(viewModel.OAuthServiceId);
            var model = service?.TokenParameters.FirstOrDefault(n => n.OAuthServiceParameterId == viewModel.OAuthServiceParameterId);
            if (service != null && model == null)
            {
                ModelState.AddModelError(nameof(viewModel.ParameterName), TextsAndMessagesHelper.IWCN_ES_ParamNotEditable);
            }

            if (model != null && ValidateParameter(service, viewModel) && ModelState.IsValid)
            {
                // Erst der Wert: lehnt die Regel ab, aendert sie nichts - und dann bleibt auch der Name, wie er war.
                if (ApplyParameterValue(service, model, viewModel))
                {
                    model.ParameterName = viewModel.ParameterName.Trim();
                    await db.SaveChangesAsync();
                }
            }

            return Json(await new[] { model != null ? ToParameterViewModel(model) : viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Services.Connections.Write)")]
        public async Task<IActionResult> DestroyParameter([DataSourceRequest] DataSourceRequest request, ExternalOAuthServiceParameterViewModel viewModel)
        {
            var service = FindServiceForParameterEdit(viewModel.OAuthServiceId);
            var model = service?.TokenParameters.FirstOrDefault(n => n.OAuthServiceParameterId == viewModel.OAuthServiceParameterId);
            if (service != null && model == null)
            {
                ModelState.AddModelError(nameof(viewModel.ParameterName), TextsAndMessagesHelper.IWCN_ES_ParamNotEditable);
            }

            if (model != null && ModelState.IsValid)
            {
                // required relationship: removing the parameter from the collection deletes the orphaned row
                service.TokenParameters.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        private TExternalOAuthService FindServiceForParameterEdit(int oauthServiceId)
        {
            var isAdmin = HttpContext.RequestServices.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });
            var service = FindEditableService(oauthServiceId, isAdmin);
            if (service == null)
            {
                ModelState.AddModelError(nameof(ExternalOAuthServiceParameterViewModel.ParameterName), TextsAndMessagesHelper.IWCN_ES_ParamNotEditable);
            }

            return service;
        }

        /// <summary>
        /// Der Dienst, wenn der Aufrufer ihn aendern darf: ein Sysadmin jeden, den er sieht, ein Mandanten-Admin nur einen
        /// des EIGENEN Mandanten.
        /// </summary>
        /// <remarks>
        /// Der Datenbankfilter allein reicht dafuer nicht: er laesst globale Dienste durch (im Baum-Modell schon immer,
        /// im flachen seit §88), und lesen duerfen Mandanten sie ja - aendern und loeschen nicht.
        /// </remarks>
        /// <returns>der Dienst, oder null (dann steht der Grund im ModelState)</returns>
        private TExternalOAuthService FindEditableService(int oauthServiceId, bool isAdmin)
        {
            var service = db.ExternalOAuthServices.FirstOrDefault(n => n.OAuthServiceId == oauthServiceId);
            if (service == null || (!isAdmin && (service.TenantId == null || service.TenantId != db.CurrentTenantId)))
            {
                ITVComponents.Logging.LogEnvironment.LogEvent(
                    $"External service {oauthServiceId} was not changed: it does not exist or is not editable for the current user.",
                    ITVComponents.Logging.LogSeverity.Warning);
                ModelState.AddModelError(nameof(ExternalOAuthServiceViewModel.UniqueConnectionName), TextsAndMessagesHelper.IWCN_ES_ParamNotEditable);
                return null;
            }

            return service;
        }

        private bool ValidateParameter(TExternalOAuthService service, ExternalOAuthServiceParameterViewModel viewModel)
        {
            var name = viewModel.ParameterName?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ModelState.AddModelError(nameof(viewModel.ParameterName), TextsAndMessagesHelper.IWCN_ES_ParamNameRequired);
                return false;
            }

            if (TokenRequestParameterRules.IsReserved(service.AuthenticationType, name))
            {
                ModelState.AddModelError(nameof(viewModel.ParameterName), string.Format(TextsAndMessagesHelper.IWCN_ES_ParamReserved, name));
                return false;
            }

            if (service.TokenParameters.Any(n => n.OAuthServiceParameterId != viewModel.OAuthServiceParameterId && string.Equals(n.ParameterName, name, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(viewModel.ParameterName), string.Format(TextsAndMessagesHelper.IWCN_ES_ParamDuplicate, name));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Wendet Wert und Geheim-Flag an (Regel: <c>OAuthExtensions.ApplyValue</c>) und meldet eine Ablehnung
        /// im ModelState.
        /// </summary>
        /// <returns>true, wenn angewendet</returns>
        private bool ApplyParameterValue(TExternalOAuthService service, ExternalOAuthServiceParameter model,
            ExternalOAuthServiceParameterViewModel viewModel)
        {
            switch (model.ApplyValue(viewModel.IsSecret, viewModel.ParameterValue, plain => EncryptFor(service, plain)))
            {
                case TokenParameterValueResult.SecretMustBeEnteredAgain:
                    ModelState.AddModelError(nameof(viewModel.ParameterValue), TextsAndMessagesHelper.IWCN_ES_ParamSecretEnterAgain);
                    return false;
                case TokenParameterValueResult.EncryptionFailed:
                    ModelState.AddModelError(nameof(viewModel.ParameterValue), TextsAndMessagesHelper.IWCN_ES_ParamSecretNotStored);
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Verschluesselt mit dem Schluessel des Dienstes - global mit dem Systemschluessel, sonst mit dem des Mandanten.
        /// Gleich fuer ClientSecret und geheime Token-Parameter, weil beide an derselben Stelle entschluesselt werden.
        /// </summary>
        /// <returns>der verschluesselte Wert, oder null, wenn der Mandant nicht gefunden wurde (protokolliert)</returns>
        private string EncryptFor(TExternalOAuthService model, string plain)
        {
            if (model.Global)
            {
                return plain.Encrypt();
            }

            var t = db.Tenants.FirstOrDefault(n => n.TenantId == model.TenantId);
            if (t == null)
            {
                ITVComponents.Logging.LogEnvironment.LogEvent(
                    $"A secret of external service '{model.UniqueConnectionName}' was not stored: tenant {model.TenantId} was not found.",
                    ITVComponents.Logging.LogSeverity.Error);
                return null;
            }

            return secRepo.Encrypt(plain, t.TenantName);
        }

        private static ExternalOAuthServiceParameterViewModel ToParameterViewModel(ExternalOAuthServiceParameter model)
        {
            return new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceParameterId = model.OAuthServiceParameterId,
                OAuthServiceId = model.OAuthServiceId,
                ParameterName = model.ParameterName,
                // Ein Geheimnis verlaesst die Datenbank nicht wieder - auch nicht verschluesselt.
                ParameterValue = model.IsSecret ? null : model.ParameterValue,
                IsSecret = model.IsSecret,
                HasValue = !string.IsNullOrEmpty(model.ParameterValue)
            };
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
