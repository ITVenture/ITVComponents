using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.EFRepo.DIIntegration.Impl;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Areas.Connectivity.Controllers
{
    [Authorize("HasPermission(Apps.PermissionSets.View,Apps.PermissionSets.Write),HasFeature(ITVAdminViews)"), Area("Connectivity"), ConstructedGenericControllerConvention]
    public class PermissionSetController<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientApp, TClientAppPermission, TClientAppAccess, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig, TContext> : Controller
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
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
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>, new ()
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>, new()
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
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

        public PermissionSetController(TContext db)
        {
            this.db = db;
            db.HideGlobals = false;
        }

        public IActionResult Index()
        {
            // Die Vorlagen fuer die Fremdschluessel-Spalte. Ohne sie gaebe es in dieser Maske keinen Weg,
            // das Pflichtfeld zu setzen - und der Knopf "Neu" fuehrte in eine Sackgasse, an deren Ende
            // der Fremdschluessel steht.
            ViewData["AppTemplates"] = db.ClientAppTemplates
                .OrderBy(n => n.Name)
                .Select(n => new ForeignKeyData<int> { Key = n.ClientAppTemplateId, Label = n.Name })
                .ToList();
            return View();
        }

        public IActionResult PermissionTable(int parentId)
        {
            return View(parentId);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.AppPermissionSets.ToDataSourceResult(request,
                n => n.ToViewModel<TAppPermissionSet, PermissionSetViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request,
            PermissionSetViewModel viewModel)
        {
            var model = new TAppPermissionSet();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<PermissionSetViewModel, TAppPermissionSet>(model);
                if (TemplateAndNameAreValid(model.ClientAppTemplateId, model.Name, 0))
                {
                    db.AppPermissionSets.Add(model);
                    await db.SaveChangesAsync();
                }
            }

            return Json(await new[] { model.ToViewModel<TAppPermissionSet, PermissionSetViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request,
            PermissionSetViewModel viewModel)
        {
            var model = db.AppPermissionSets.First(n => n.AppPermissionSetId == viewModel.AppPermissionSetId);
            if (ModelState.IsValid)
            {
                db.AppPermissionSets.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request,
            PermissionSetViewModel viewModel)
        {
            var model = db.AppPermissionSets.First(n => n.AppPermissionSetId == viewModel.AppPermissionSetId);
            if (ModelState.IsValid)
            {
                var previousTemplateId = model.ClientAppTemplateId;
                await this.TryUpdateModelAsync<PermissionSetViewModel, TAppPermissionSet>(model, "",
                    m => { return m.ElementType == null; });
                if (TemplateAndNameAreValid(model.ClientAppTemplateId, model.Name, model.AppPermissionSetId)
                    && MoveIsAllowed(previousTemplateId, model.ClientAppTemplateId, model.AppPermissionSetId))
                {
                    await db.SaveChangesAsync();
                }
                else
                {
                    // Die Aenderungen stehen bereits an der verfolgten Entitaet - ohne dieses Zuruecklesen
                    // gingen sie beim naechsten SaveChanges trotz der Abweisung hinaus.
                    await db.Entry(model).ReloadAsync();
                }
            }

            return Json(await new[] { model.ToViewModel<TAppPermissionSet, PermissionSetViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        /// <summary>
        /// Prueft vorab, was sonst erst die Datenbank abweist - dort aber ohne Aussage.
        /// </summary>
        /// <remarks>
        /// Der Fremdschluessel auf das Template und der eindeutige Index
        /// <c>UQ_AppPermissionSetName (ClientAppTemplateId, Name)</c> bleiben die eigentliche Absicherung;
        /// diese Pruefung ersetzt sie nicht, sie macht aus dem Fehlschlag eine Meldung, mit der ein
        /// Benutzer etwas anfangen kann.
        /// </remarks>
        private bool TemplateAndNameAreValid(int clientAppTemplateId, string name, int exceptSetId)
        {
            if (clientAppTemplateId <= 0 ||
                !db.ClientAppTemplates.Any(n => n.ClientAppTemplateId == clientAppTemplateId))
            {
                LogEnvironment.LogEvent(
                    $"The permission-set '{name}' was refused: app-template {clientAppTemplateId} does not exist.",
                    LogSeverity.Error);
                ModelState.AddModelError(nameof(PermissionSetViewModel.ClientAppTemplateId),
                    "A valid template is required.");
                return false;
            }

            if (db.AppPermissionSets.Any(n => n.ClientAppTemplateId == clientAppTemplateId && n.Name == name &&
                                              n.AppPermissionSetId != exceptSetId))
            {
                LogEnvironment.LogEvent(
                    $"The permission-set '{name}' was refused: app-template {clientAppTemplateId} already has a set of that name.",
                    LogSeverity.Error);
                ModelState.AddModelError(nameof(PermissionSetViewModel.Name),
                    "This template already has a permission set of that name.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Ob das Buendel einem anderen Template zugeordnet werden darf.
        /// </summary>
        /// <remarks>
        /// Der Wechsel ist erlaubt (ein falsch einsortiertes Buendel muss man verschieben koennen),
        /// <b>aber er verschiebt die Obergrenze</b>: eine ClientApp darf nur Buendel ihres eigenen
        /// Templates fuehren. Fuehrt eine Anwendung dieses Buendel bereits, verloere sie durch den Wechsel
        /// stillschweigend ihre Rechte - dieselbe Ueberlegung wie beim Loeschen.
        /// </remarks>
        private bool MoveIsAllowed(int previousTemplateId, int newTemplateId, int appPermissionSetId)
        {
            if (previousTemplateId == newTemplateId ||
                !db.ClientAppPermissions.Any(n => n.AppPermissionSetId == appPermissionSetId))
            {
                return true;
            }

            LogEnvironment.LogEvent(
                $"Moving permission-set {appPermissionSetId} from app-template {previousTemplateId} to {newTemplateId} was refused: it is still granted to at least one client-app.",
                LogSeverity.Error);
            ModelState.AddModelError(nameof(PermissionSetViewModel.ClientAppTemplateId),
                "The permission set is still granted to a client app and cannot be moved to another template.");
            return false;
        }

        public async Task<IActionResult> ReadPermissions([DataSourceRequest]DataSourceRequest request, int parentId)
        {
            var perms = (from prm in db.Permissions
                join apr in db.AppPermissions.Where(n => n.AppPermissionSetId == parentId) on prm.PermissionId equals
                    apr.PermissionId
                    into apg
                from j in apg.DefaultIfEmpty()
                         where prm.TenantId == null
                // j ist null, wenn die Berechtigung NICHT im Buendel liegt - "Selected" war also genau
                // verdreht: angehakt erschien, was gerade nicht zugeordnet ist.
                select new { Selected = j != null, prm.PermissionId, prm.PermissionName, prm.Description });
            return Json(perms.ToDataSourceResult(request, s => new PermissionViewModel
            {
                Assigned = s.Selected,
                Description = s.Description,
                Editable = false,
                IsGlobal = true,
                PermissionId = s.PermissionId,
                TenantId = null,
                PermissionName = s.PermissionName,
                RoleId = parentId,
                UniQUID = $"UQPS{s.PermissionId}_{parentId}"
            }));
        }

        /// <summary>
        /// Setzt oder entfernt eine Berechtigung in einem Rechtebuendel.
        /// </summary>
        /// <remarks>
        /// <c>RoleId</c> traegt hier die <b>AppPermissionSetId</b> - das Sichtmodell ist von der
        /// Rollen-Maske uebernommen. Bis PRE242 zeigte das Raster deshalb auf <c>Update</c> am
        /// Permission-Controller, und der schlug die Kennung in <c>SecurityRoles</c> nach.
        /// </remarks>
        [HttpPost]
        [Authorize("HasPermission(Apps.PermissionSets.Write,Apps.PermissionSets.AssignPermission)")]
        public async Task<IActionResult> UpdatePermission([DataSourceRequest] DataSourceRequest request,
            PermissionViewModel mdl)
        {
            if (mdl.RoleId == null)
            {
                LogEnvironment.LogEvent(
                    $"Assigning permission {mdl.PermissionId} was refused: no permission-set was named.",
                    LogSeverity.Warning);
                ModelState.AddModelError("", "No permission set was named.");
                return Json(new[] { mdl }.ToDataSourceResult(request, ModelState));
            }

            var entity = db.AppPermissions.FirstOrDefault(n =>
                n.PermissionId == mdl.PermissionId && n.AppPermissionSetId == mdl.RoleId);
            var isAssigned = entity != null;
            if (isAssigned != mdl.Assigned)
            {
                if (isAssigned)
                {
                    db.AppPermissions.Remove(entity);
                }
                else
                {
                    db.AppPermissions.Add(new TAppPermission
                    {
                        PermissionId = mdl.PermissionId,
                        AppPermissionSetId = mdl.RoleId.Value
                    });
                }

                await db.SaveChangesAsync();
            }

            return Json(new[] { mdl }.ToDataSourceResult(request));
        }
    }
}
