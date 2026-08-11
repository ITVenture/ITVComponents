using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class DbDiagnosticsQueryStore<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IDiagnosticsStore
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
        where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>, new()
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TTenant : Tenant
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation:TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        /// <summary>
        /// Per-operation factory for the security context (Blazor-safe: a fresh, short-lived context per call instead
        /// of a shared circuit-scoped one).
        /// </summary>
        private readonly IToolkitContextFactory contextFactory;

        public DbDiagnosticsQueryStore(IToolkitContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }

        /// <summary>
        /// Leases a fresh per-operation security context for the duration of a single operation.
        /// </summary>
        private IContextLease<ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> LeaseDb()
            => contextFactory.Lease<ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();

        /// <summary>
        /// Hook invoked on each freshly-leased per-operation context before it is used. The default implementation does
        /// nothing; derived strategies (e.g. the hierarchy/tree store) override this to apply their trust-configuration
        /// (via <c>ISecurityAccessProvider.CreateForCaller</c>) to the very context instance the base will read from,
        /// returning the resulting helper so its scope is disposed when the operation ends.
        /// </summary>
        /// <param name="dbContext">the freshly-leased per-operation context that the operation will use</param>
        /// <returns>a disposable representing the applied scope, or null when no scope is applied</returns>
        protected virtual IDisposable AcquireScope(ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> dbContext)
            => null;

        /// <summary>
        /// Finds the demanded DiagnosticsQuery and returns it including Query-Arguments
        /// </summary>
        /// <param name="queryName">the name of the requested DiagnosticsQuery</param>
        /// <returns>a DiagnosticsQueryDefinition-Object containing all parameters and permissions required to execute it</returns>
        public virtual DiagnosticsQueryDefinition GetQuery(string queryName)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            using var scope = AcquireScope(dbContext);
            return GetQuery(queryName, dbContext);
        }

        /// <summary>
        /// Finds the demanded DiagnosticsQuery on the given (already-leased) context. Used both by the public entry-point
        /// and by internal callers that already hold a leased context, so they do not lease a second one.
        /// </summary>
        private DiagnosticsQueryDefinition GetQuery(string queryName, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> dbContext)
        {
            var dbQuery = dbContext.DiagnosticsQueries.FirstOrDefault(n => n.DiagnosticsQueryName.ToLower() == queryName.ToLower());
            if (dbQuery != null)
            {
                var retVal = new DiagnosticsQueryDefinition
                {
                    AutoReturn = dbQuery.AutoReturn,
                    DbContext = dbQuery.DbContext,
                    DiagnosticsQueryName = dbQuery.DiagnosticsQueryName,
                    Permission = dbQuery.Permission.PermissionName,
                    QueryText = dbQuery.QueryText
                };
                foreach (var diagnosticsQueryParameter in dbQuery.Parameters)
                {
                    retVal.Parameters.Add(new DiagnosticsQueryParameterDefinition
                    {
                        DefaultValue = diagnosticsQueryParameter.DefaultValue,
                        Format = diagnosticsQueryParameter.Format,
                        Optional = diagnosticsQueryParameter.Optional,
                        ParameterName = diagnosticsQueryParameter.ParameterName,
                        ParameterType = diagnosticsQueryParameter.ParameterType
                    });
                }

                return retVal;
            }

            return null;
        }

        /// <summary>
        /// Finds the demanded Dashboard-Item and returns it
        /// </summary>
        /// <param name="dashboardName">the name of the requested dashboard-item</param>
        /// <param name="targetCulture">the culture that is used to select the basic-markup for the requested dashboard-template</param>
        /// <param name="userDashboardId">the id of the user-dashboard that is requested</param>
        /// <returns>the definition of the requested dashboard-item including the permissions required to use it</returns>
        public virtual DashboardWidgetDefinition GetDashboard(string dashboardName, string targetCulture, int? userDashboardId = null)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            using var scope = AcquireScope(dbContext);
            var tmp = dbContext.Widgets.FirstOrDefault(n => n.SystemName == dashboardName);
            var userDash = (userDashboardId != null)
                ? dbContext.UserWidgets.FirstOrDefault(n => n.UserWidgetId == userDashboardId)
                : null;
            if (tmp != null && (userDashboardId == null || userDash != null))
            {
                var retVal = GetDashboardItem(tmp, userDash, targetCulture, dbContext);
                if (userDashboardId == null)
                {
                    retVal.SortOrder = dbContext.UserWidgets.Count();
                }
                else
                {
                    retVal.SortOrder = userDash.SortOrder;
                }

                return retVal;
            }

            return null;
        }

        /// <summary>
        /// Sets the User-Widgets for the given user
        /// </summary>
        /// <param name="userWidgets">the target widgets to add</param>
        /// <param name="userName">the user for which to register these widgets</param>
        /// <returns>an empty task</returns>
        public virtual async Task<DashboardWidgetDefinition[]> SetUserWidgets(DashboardWidgetDefinition[] widgets, string userName)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            using var scope = AcquireScope(dbContext);
            var tmp = dbContext.ShowAllTenants;
            try
            {
                dbContext.ShowAllTenants = false;
                if (dbContext.CurrentTenantId != null)
                {
                    var tenantId = dbContext.CurrentTenantId.Value;
                    var dbwidgets = dbContext.UserWidgets.OrderBy(n => n.SortOrder).ToArray();
                    var allIds = dbwidgets.Select(n => n.UserWidgetId)
                        .Union(widgets.Select(n => n.UserWidgetId)).Distinct().ToArray();
                    var selection = (from i in allIds
                        join d in dbwidgets on i equals d.UserWidgetId into j1
                        from lj1 in j1.DefaultIfEmpty()
                        join p in widgets on i equals p.UserWidgetId into j2
                        from lj2 in j2.DefaultIfEmpty()
                        select new { Id = i, Db = lj1, Posted = lj2 }).ToArray();
                    foreach (var item in selection)
                    {
                        if (item.Db == null)
                        {
                            var w = dbContext.Widgets.First(n => n.DashboardWidgetId == item.Posted.DashboardWidgetId);
                            dbContext.UserWidgets.Add(new TUserWidget()
                            {
                                DashboardWidgetId = item.Posted.DashboardWidgetId,
                                // Die gepostete Sortierung uebernehmen, nicht die aktuelle Anzahl: die
                                // aendert sich vor dem SaveChanges nicht, also bekamen mehrere neue
                                // Kacheln aus einem Aufruf alle DIESELBE Position - und genau so wird die
                                // Standard-Sammlung angelegt.
                                SortOrder = item.Posted.SortOrder,
                                ColSpan = item.Posted.ColSpan,
                                TenantId = tenantId,
                                UserName = userName,
                                CustomQueryString = w.Params.Any() ? item.Posted.CustomQueryString : null,
                                ParamValues = w.Params.Any() ? item.Posted.ParamValues : null,
                                DisplayName = !string.IsNullOrEmpty(w.TitleTemplate) ? item.Posted.DisplayName : null
                            });
                        }
                        else if (item.Posted == null)
                        {
                            dbContext.UserWidgets.Remove(item.Db);
                        }
                        else
                        {
                            item.Db.SortOrder = item.Posted.SortOrder;
                            item.Db.ColSpan = item.Posted.ColSpan;
                            if (item.Db.Widget.Params.Any())
                            {
                                item.Db.CustomQueryString
                                    = item.Posted.CustomQueryString;
                                item.Db.ParamValues = item.Posted.ParamValues;
                            }

                            if (!string.IsNullOrEmpty(item.Db.Widget.TitleTemplate))
                            {
                                // Der Titel wird aus dem TitleTemplate mit den Parametern gebildet -
                                // aendern sich die Parameter, muss er mitgehen.
                                item.Db.DisplayName = item.Posted.DisplayName;
                            }
                        }
                    }

                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                    var ret = dbContext.UserWidgets.OrderBy(n => n.SortOrder).ToArray()
                        .Select(n => new DashboardWidgetDefinition
                        {
                            UserWidgetId = n.UserWidgetId,
                            CustomQueryString = n.Widget.Params.Any()
                                ? n.CustomQueryString
                                : n.Widget.CustomQueryString,
                            SortOrder = n.SortOrder,
                            ColSpan = n.ColSpan,
                            ParamValues = n.ParamValues,
                            DashboardWidgetId = n.DashboardWidgetId,
                            DisplayName = n.DisplayName ?? n.Widget.DisplayName,
                            TitleTemplate = n.Widget.TitleTemplate,
                            DiagnosticsQuery = GetQuery(n.Widget.DiagnosticsQuery.DiagnosticsQueryName, dbContext),
                            SystemName = n.Widget.SystemName,
                            Template = n.Widget.Template,
                            Area = n.Widget.Area,
                            InitiallyActive = n.Widget.InitiallyActive

                        }).ToArray();
                    return ret;
                }
            }
            finally
            {
                dbContext.ShowAllTenants = tmp;
            }

            return null;
        }

        /// <summary>
        /// Gets an array containing all defined DashboardWidget-Definitions
        /// </summary>
        /// <returns>an array that contains all known dashboard-templates</returns>
        public virtual DashboardWidgetDefinition[] GetWidgetTemplates(string targetCulture)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            using var scope = AcquireScope(dbContext);
            // Nach SortOrder VOR dem Namen: die Reihenfolge der Standard-Sammlung wird am Widget gepflegt,
            // und in genau dieser Reihenfolge werden die Kacheln fuer einen neuen Benutzer angelegt.
            return (from t in dbContext.Widgets.ToArray()
                orderby t.SortOrder, t.DisplayName
                select GetDashboardItem(t,null, targetCulture, dbContext)).ToArray();
        }

        /// <summary>
        /// Gets an array containing all User-Widgets
        /// </summary>
        /// <returns>an array that contains all assigned user-widgets.</returns>
        public virtual DashboardWidgetDefinition[] GetUserWidgets(string userName, string targetCulture)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            using var scope = AcquireScope(dbContext);
            var tmp = dbContext.ShowAllTenants;
            try
            {
                var tmpUw = dbContext.UserWidgets.OrderBy(n => n.SortOrder).ToArray();
                return (from t in tmpUw
                    select GetDashboardItem(t.Widget,t, targetCulture, dbContext)).ToArray();
            }
            finally
            {
                dbContext.ShowAllTenants = tmp;
            }
        }

        /// <summary>
        /// Creates a basic-definition of a Widget-Definition from a DashbaordWidget entity
        /// </summary>
        /// <param name="tmp">the entity from which to create the definition</param>
        /// <returns>a complete widget-definition</returns>
        private  DashboardWidgetDefinition GetDashboardItem(TWidget tmp, TUserWidget userWidget, string targetCulture, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> dbContext)
        {
            IDashboardRawDefinition lng = !string.IsNullOrEmpty(targetCulture)
                ? tmp.Localizations.FirstOrDefault(n => n.LocaleName == targetCulture)
                : null;
            if (lng == null && !string.IsNullOrEmpty(targetCulture) && targetCulture.Contains("-"))
            {
                var loca = targetCulture.Substring(0, targetCulture.IndexOf("-"));
                lng = tmp.Localizations.FirstOrDefault(n => n.LocaleName == loca);
            }

            lng ??= tmp;
            var retVal = new DashboardWidgetDefinition
            {
                Area = tmp.Area,
                CustomQueryString = userWidget?.CustomQueryString??tmp.CustomQueryString,
                DiagnosticsQuery = GetQuery(tmp.DiagnosticsQuery.DiagnosticsQueryName, dbContext),
                DisplayName = (userWidget?.DisplayName??lng.DisplayName).Translate(targetCulture),
                SystemName = tmp.SystemName,
                Template = lng.Template,
                TitleTemplate = lng.TitleTemplate,
                // Aus tmp und NICHT aus lng: der Konfigurationstext darf je Sprache abweichen, der
                // Renderer nicht - sonst zeichnete dieselbe Kachel je nach Sprache etwas anderes.
                RendererKey = tmp.RendererKey,
                RendererOptions = tmp.RendererOptions,
                UserWidgetId = userWidget?.UserWidgetId??0,
                DashboardWidgetId = tmp.DashboardWidgetId,
                // Die Sortierung wurde hier bisher NICHT uebernommen - die Reihenfolge steckte allein in
                // der Reihenfolge des zurueckgegebenen Arrays und ging bei jedem Rueckweg verloren. Zum
                // Umordnen und Speichern muss der Wert mitkommen.
                SortOrder = userWidget?.SortOrder ?? tmp.SortOrder,
                ColSpan = userWidget?.ColSpan ?? 0,
                ParamValues = userWidget?.ParamValues,
                InitiallyActive = tmp.InitiallyActive
            };

            if (userWidget == null)
            {
                foreach (var param in tmp.Params)
                {
                    retVal.Params.Add(GetDashboardParamItem(param));
                }
            }

            return retVal;
        }

        /// <summary>
        /// Creates a basic-definition of a Widget-Param-Definition from a DashbaordParam entity
        /// </summary>
        /// <param name="tmp">the entity from which to create the definition</param>
        /// <returns>a complete param-definition</returns>
        private DashboardParamDefinition GetDashboardParamItem(TWidgetParam param)
        {
            return new DashboardParamDefinition
            {
                InputConfig = param.InputConfig,
                InputType = param.InputType,
                ParameterName = param.ParameterName
            };
        }
    }
}
