﻿using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class DbDiagnosticsQueryStore<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser> : IDiagnosticsStore
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TNavigationMenu : NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>, new()
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TAppPermission : AppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    {
        private readonly ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser> dbContext;

        public DbDiagnosticsQueryStore(ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser> dbContext)
        {
            this.dbContext = dbContext;
        }

        /// <summary>
        /// Finds the demanded DiagnosticsQuery and returns it including Query-Arguments
        /// </summary>
        /// <param name="queryName">the name of the requested DiagnosticsQuery</param>
        /// <returns>a DiagnosticsQueryDefinition-Object containing all parameters and permissions required to execute it</returns>
        public DiagnosticsQueryDefinition GetQuery(string queryName)
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
        public DashboardWidgetDefinition GetDashboard(string dashboardName, string targetCulture, int? userDashboardId = null)
        {
            var tmp = dbContext.Widgets.FirstOrDefault(n => n.SystemName == dashboardName);
            var userDash = (userDashboardId != null)
                ? dbContext.UserWidgets.FirstOrDefault(n => n.UserWidgetId == userDashboardId)
                : null;
            if (tmp != null && (userDashboardId == null || userDash != null))
            {
                var retVal = GetDashboardItem(tmp, userDash, targetCulture);
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
        public async Task<DashboardWidgetDefinition[]> SetUserWidgets(DashboardWidgetDefinition[] widgets, string userName)
        {
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
                                SortOrder = dbContext.UserWidgets.Count(),
                                TenantId = tenantId,
                                UserName = userName,
                                CustomQueryString = w.Params.Any() ? item.Posted.CustomQueryString : null,
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
                            if (item.Db.Widget.Params.Any())
                            {
                                item.Db.CustomQueryString
                                    = item.Posted.CustomQueryString;
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
                            DashboardWidgetId = n.DashboardWidgetId,
                            DisplayName = n.DisplayName ?? n.Widget.DisplayName,
                            TitleTemplate = n.Widget.TitleTemplate,
                            DiagnosticsQuery = GetQuery(n.Widget.DiagnosticsQuery.DiagnosticsQueryName),
                            SystemName = n.Widget.SystemName,
                            Template = n.Widget.Template,
                            Area = n.Widget.Area

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
        public DashboardWidgetDefinition[] GetWidgetTemplates(string targetCulture)
        {
            return (from t in dbContext.Widgets.ToArray()
                orderby t.DisplayName
                select GetDashboardItem(t,null, targetCulture)).ToArray();
        }

        /// <summary>
        /// Gets an array containing all User-Widgets
        /// </summary>
        /// <returns>an array that contains all assigned user-widgets.</returns>
        public DashboardWidgetDefinition[] GetUserWidgets(string userName, string targetCulture)
        {
            var tmp = dbContext.ShowAllTenants;
            try
            {
                var tmpUw = dbContext.UserWidgets.OrderBy(n => n.SortOrder).ToArray();
                return (from t in tmpUw
                    select GetDashboardItem(t.Widget,t, targetCulture)).ToArray();
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
        private DashboardWidgetDefinition GetDashboardItem(TWidget tmp, TUserWidget userWidget, string targetCulture)
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
                DiagnosticsQuery = GetQuery(tmp.DiagnosticsQuery.DiagnosticsQueryName),
                DisplayName = userWidget?.DisplayName??lng.DisplayName,
                SystemName = tmp.SystemName,
                Template = lng.Template,
                TitleTemplate = lng.TitleTemplate,
                UserWidgetId = userWidget?.UserWidgetId??0,
                DashboardWidgetId = tmp.DashboardWidgetId
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
