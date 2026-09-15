using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Areas.Connectivity.Controllers
{
    [Authorize("HasPermission(Apps.Templates.View,Apps.Templates.Write),HasFeature(ITVAdminViews)"), Area("Connectivity"), ConstructedGenericControllerConvention]
    public class AppTemplateController<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientApp, TClientAppPermission, TClientAppAccess, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig, TContext> : Controller
        where TRole : Role<TTenant,TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>, new()
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>, new()
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
        where TClientAppAccess : ClientAppAccess<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
        where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientApp, TClientAppPermission, TClientAppAccess, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
        where TTenant : Tenant
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        private readonly TContext db;

        public AppTemplateController(TContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult PermissionTable(int parentId)
        {
            return View(parentId);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.ClientAppTemplates.ToDataSourceResult(request,
                n => n.ToViewModel<TClientAppTemplate, ClientAppTemplateViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request, ClientAppTemplateViewModel viewModel)
        {
            var model = new TClientAppTemplate();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<ClientAppTemplateViewModel, TClientAppTemplate>(model);
                db.ClientAppTemplates.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TClientAppTemplate, ClientAppTemplateViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request,
            ClientAppTemplateViewModel viewModel)
        {
            var model = db.ClientAppTemplates.First(n => n.ClientAppTemplateId == viewModel.ClientAppTemplateId);
            if (ModelState.IsValid)
            {
                db.ClientAppTemplates.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request,
            ClientAppTemplateViewModel viewModel)
        {
            var model = db.ClientAppTemplates.First(n => n.ClientAppTemplateId== viewModel.ClientAppTemplateId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<ClientAppTemplateViewModel, TClientAppTemplate>(model, "",
                    m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TClientAppTemplate, ClientAppTemplateViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        public async Task<IActionResult> ReadPermissions([DataSourceRequest] DataSourceRequest request, int parentId)
        {
            // Ein Rechtebuendel gehoert direkt zu genau einem Template; die frueher global geteilten
            // Buendel samt Zuordnungstabelle gibt es nicht mehr. Was diese Liste zeigt, GEHOERT dem
            // Template - es gibt nichts zu- oder abzuwaehlen, nur anzulegen, umzubenennen und zu loeschen.
            var perms = from prm in db.AppPermissionSets
                        where prm.ClientAppTemplateId == parentId
                        select new { prm.AppPermissionSetId, prm.Name };
            return Json(perms.ToDataSourceResult(request, s => new AppPermissionViewModel
            {
                ParentId = parentId,
                AppPermissionSetId = s.AppPermissionSetId,
                PermissionSetName = s.Name
            }));
        }

        /// <summary>
        /// Legt ein Rechtebuendel unter diesem Template an.
        /// </summary>
        /// <remarks>
        /// Der Weg, den es bis PRE241 nirgends gab: das Kindgitter konnte auflisten und loeschen, und der
        /// Weg ueber die Buendel-Maske war seinerseits kaputt, weil er das Template nicht setzte.
        /// <para>
        /// Das Template kommt aus der <b>Route</b> und nicht aus der geposteten Zeile: es steht in der
        /// Adresse, die der Server selbst gerendert hat.
        /// </para>
        /// </remarks>
        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write)")]
        public async Task<IActionResult> CreatePermission([DataSourceRequest] DataSourceRequest request,
            AppPermissionViewModel mdl, int parentId)
        {
            mdl.ParentId = parentId;
            if (ModelState.IsValid && SetNameIsValid(parentId, mdl.PermissionSetName, 0))
            {
                var entity = new TAppPermissionSet
                {
                    Name = mdl.PermissionSetName.Trim(),
                    ClientAppTemplateId = parentId
                };
                db.AppPermissionSets.Add(entity);
                await db.SaveChangesAsync();
                mdl.AppPermissionSetId = entity.AppPermissionSetId;
                mdl.PermissionSetName = entity.Name;
            }

            return Json(new[] { mdl }.ToDataSourceResult(request, ModelState));
        }

        /// <summary>
        /// Benennt ein Rechtebuendel dieses Templates um.
        /// </summary>
        /// <remarks>
        /// Nur der Name. Das Template zu wechseln verschoebe die Obergrenze einer bereits ausgestatteten
        /// Anwendung und gehoert deshalb nicht in das Kindgitter des Templates, in dem man gerade steht -
        /// dafuer gibt es die Buendel-Maske.
        /// </remarks>
        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write)")]
        public async Task<IActionResult> UpdatePermission([DataSourceRequest] DataSourceRequest request,
            AppPermissionViewModel mdl, int parentId)
        {
            mdl.ParentId = parentId;
            var entity = db.AppPermissionSets.FirstOrDefault(n =>
                n.AppPermissionSetId == mdl.AppPermissionSetId && n.ClientAppTemplateId == parentId);
            if (entity == null)
            {
                LogEnvironment.LogEvent(
                    $"Permission-set {mdl.AppPermissionSetId} does not exist below app-template {parentId}; nothing renamed.",
                    LogSeverity.Warning);
                ModelState.AddModelError("", "The permission set does not belong to this template.");
            }
            else if (ModelState.IsValid && SetNameIsValid(parentId, mdl.PermissionSetName, mdl.AppPermissionSetId))
            {
                entity.Name = mdl.PermissionSetName.Trim();
                await db.SaveChangesAsync();
                mdl.PermissionSetName = entity.Name;
            }

            return Json(new[] { mdl }.ToDataSourceResult(request, ModelState));
        }

        /// <summary>
        /// Entfernen heisst LOESCHEN: das Buendel gehoert genau diesem Template, eine Zuordnung, die man
        /// loesen koennte, gibt es nicht mehr. Ein Buendel, das eine ClientApp noch fuehrt, bleibt stehen -
        /// sonst verloere eine laufende Anwendung stillschweigend ihre Rechte.
        /// </summary>
        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write)")]
        public async Task<IActionResult> DestroyPermission([DataSourceRequest] DataSourceRequest request,
            AppPermissionViewModel mdl, int parentId)
        {
            mdl.ParentId = parentId;
            var entity = db.AppPermissionSets.FirstOrDefault(n =>
                n.AppPermissionSetId == mdl.AppPermissionSetId && n.ClientAppTemplateId == parentId);
            if (entity == null)
            {
                LogEnvironment.LogEvent(
                    $"Permission-set {mdl.AppPermissionSetId} does not exist below app-template {parentId}; nothing deleted.",
                    LogSeverity.Warning);
            }
            else if (db.ClientAppPermissions.Any(n => n.AppPermissionSetId == mdl.AppPermissionSetId))
            {
                LogEnvironment.LogEvent(
                    $"Permission-set {mdl.AppPermissionSetId} of app-template {parentId} is still granted to at least one client-app; not deleted.",
                    LogSeverity.Warning);
                ModelState.AddModelError("", "The permission set is still granted to a client app.");
            }
            else
            {
                db.AppPermissionSets.Remove(entity);
                await db.SaveChangesAsync();
            }

            return Json(new[] { mdl }.ToDataSourceResult(request, ModelState));
        }

        /// <summary>
        /// Prueft vorab, was sonst erst der eindeutige Index abweist - dort aber ohne Aussage.
        /// </summary>
        /// <remarks>
        /// <c>UQ_AppPermissionSetName (ClientAppTemplateId, Name)</c> bleibt die eigentliche Absicherung;
        /// diese Pruefung ersetzt sie nicht, sie macht aus dem Fehlschlag eine Meldung, mit der ein
        /// Benutzer etwas anfangen kann.
        /// </remarks>
        private bool SetNameIsValid(int clientAppTemplateId, string name, int exceptSetId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                LogEnvironment.LogEvent(
                    $"A permission-set below app-template {clientAppTemplateId} was refused: the name is empty.",
                    LogSeverity.Warning);
                ModelState.AddModelError(nameof(AppPermissionViewModel.PermissionSetName), "A name is required.");
                return false;
            }

            name = name.Trim();
            if (db.AppPermissionSets.Any(n => n.ClientAppTemplateId == clientAppTemplateId && n.Name == name &&
                                              n.AppPermissionSetId != exceptSetId))
            {
                LogEnvironment.LogEvent(
                    $"The permission-set '{name}' was refused: app-template {clientAppTemplateId} already has a set of that name.",
                    LogSeverity.Error);
                ModelState.AddModelError(nameof(AppPermissionViewModel.PermissionSetName),
                    "This template already has a permission set of that name.");
                return false;
            }

            return true;
        }
    }
}
