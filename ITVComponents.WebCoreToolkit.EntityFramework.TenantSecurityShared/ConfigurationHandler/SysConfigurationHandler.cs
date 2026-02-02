using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.EFRepo.DataSync;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Plugins.Initialization;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Feature = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Feature;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ConfigurationHandler
{
    public abstract class SysConfigurationHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : ConfigurationHandlerBase
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
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TTenant : Tenant
        where TWebPlugin:WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant:WebPluginConstant<TTenant>
        where TWebPluginGenericParameter:WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence:Sequence<TTenant>
        where TTenantSetting:TenantSetting<TTenant>
        where TTenantFeatureActivation:TenantFeatureActivation<TTenant>
        where TContext:DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {

        public SysConfigurationHandler(TContext db)
        {
            DbContext = db;
        }

        private TContext DbContext { get; }

        /// <summary>
        /// Provides a list of Permissions that a user must have any of, to perform a specific task
        /// </summary>
        /// <param name="reason">the reason why this component is being invoked</param>
        /// <returns>a list of required permissions</returns>
        public override string[] PermissionsForReason(string reason)
        {
            switch (reason)
            {
                case "ReadSysConfig":
                    return new[] { "Sysadmin" };
                case "CompareSysConfig":
                    return new[]
                    {
                        "Sysadmin"
                    };
            }

            return null;
        }

        protected override void PerformCompareInternal(string fileType, byte[] content)
        {
            switch (fileType)
            {
                case "sysCfg":
                    using (var h = new FullSecurityAccessHelper<TTrustConfig>(DbContext, new() { ShowAllTenants = true, HideGlobals = false }))
                    {
                        var sys = DescribeSystem();
                        var upSys =
                            JsonHelper.FromJsonString<SystemTemplateMarkup>(
                                Encoding.UTF8.GetString(content), SerializationTypingMode.NativePolymorphism);
                        ComparePlugIns(sys.PlugIns, upSys.PlugIns);
                        CompareConstants(sys.Constants, upSys.Constants);
                        ComparePermissions(sys.Permissions, upSys.Permissions);
                        CompareGlobalRoles(sys.GlobalRoles, upSys.GlobalRoles);
                        CompareAuthenticationTypes(sys.AuthenticationTypes, upSys.AuthenticationTypes);
                        CompareAuthenticationTypeClaims(sys.AuthenticationTypeClaimTemplates, upSys.AuthenticationTypeClaimTemplates);
                        CompareGlobalSettings(sys.Settings, upSys.Settings);
                        CompareFeatures(sys.Features, upSys.Features);
                        CompareTenantTemplates(sys.TenantTemplates, upSys.TenantTemplates);
                        CompareDiagnosticsQueries(sys.DiagnosticsQueries, upSys.DiagnosticsQueries);
                        CompareDashboardWidgets(sys.DashboardWidgets, upSys.DashboardWidgets);
                        CompareDashboardWidgetLocales(sys.DashboardWidgetLocales, upSys.DashboardWidgetLocales);
                        CompareNavigation(sys.Navigation, upSys.Navigation);
                        CompareTrustedModules(sys.TrustedModules, upSys.TrustedModules);
                        CompareHealthScripts(sys.HealthScripts, upSys.HealthScripts);
                        CompareAssetTemplates(sys.AssetTemplates, upSys.AssetTemplates);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"{fileType} not supported");
            }
        }

        public override object DescribeConfig(string fileType, IDictionary<string, int> filterDic, out string name)
        {
            switch (fileType)
            {
                case "sysCfg":
                    var sys = DescribeSystem();
                    name= $"System";
                    return sys;
                default:
                    throw new InvalidOperationException($"{fileType} not supported");
            }
        }

        /// <summary>
        /// Applies changes that were generated during a comparison between two systems
        /// </summary>
        /// <param name="changes">the changes to apply on the target system</param>
        /// <param name="messages">a stringbuilder that collects all generated messages</param>
        /// <param name="extendQuery">an action that can provide query extensions if required</param>
        public override void ApplyChanges(IEnumerable<Change> changes, StringBuilder messages, Action<string, Dictionary<string, object>> extendQuery = null)
        {
            using (var h = new FullSecurityAccessHelper<TTrustConfig>(DbContext, new(){ShowAllTenants = true,HideGlobals = false}))
            {
                DbContext.ApplyData(changes.ToArray(), messages, extendQuery, null);
            }
        }

        private SystemTemplateMarkup DescribeSystem()
        {
            using (var h = new FullSecurityAccessHelper<TTrustConfig>(DbContext, new() { ShowAllTenants = true, HideGlobals = false }))
            {
                DbContext.EnsureNavUniqueness();
                SystemTemplateMarkup retVal = new SystemTemplateMarkup
                {
                    Permissions = DbContext.Permissions.Where(n => n.TenantId == null).AsEnumerable().Select(n =>
                        SelectPermissionTemplateMarkup(n)).ToArray(),
                    GlobalRoles = DbContext.GlobalRoles.Include(n => n.RolePermissions).ThenInclude(n => n.Permission)
                        .AsEnumerable()
                        .Select(r => SelectGlobalRoleTemplateMarkup(r)).ToArray(),
                    AuthenticationTypes = DbContext.AuthenticationTypes.AsEnumerable().Select(n => SelectAuthenticationTypeTemplateMarkup(n)).ToArray(),
                    AuthenticationTypeClaimTemplates = DbContext.AuthenticationClaimMappings.AsEnumerable().Select(n => SelectAuthenticationTypeClaimTemplateMarkup(n)).ToArray(),
                    Constants = DbContext.WebPluginConstants.Where(n => n.TenantId == null).AsEnumerable()
                        .Select(n => SelectConstTemplateMarkup(n)).ToArray(),
                    PlugIns = DbContext.WebPlugins.Include(n => n.Parameters).Where(n => n.TenantId == null).AsEnumerable().Select(n => SelectPlugInTemplateMarkup(n)).ToArray(),
                    Settings = DbContext.GlobalSettings.AsEnumerable().Select(n => SelectSettingTemplateMarkup(n))
                        .ToArray(),
                    TenantTemplates = DbContext.TenantTemplates.AsEnumerable().Select(n => SelectTenantTemplateDefinitionMarkup(n)).ToArray(),
                    Features = DbContext.Features.AsEnumerable().Select(n => SelectSystemFeatureTemplateMarkup(n))
                        .ToArray(),
                    DiagnosticsQueries = DbContext.DiagnosticsQueries.Include(n => n.Parameters).AsEnumerable().Select(n => SelectDiagnosticsQueryTemplateMarkup(n)).ToArray(),
                    DashboardWidgets = DbContext.Widgets.AsEnumerable().Select(n => SelectDashboardWidgetTemplateMarkup(n)).ToArray(),
                    DashboardWidgetLocales = DbContext.WidgetLocales.AsEnumerable().Select(l => SelectDashboardWidgetLocaleTemplateMarkup(l))
                        .ToArray(),
                    Navigation = GetSortedNav(),
                    TrustedModules = DbContext.TrustedFullAccessComponents.AsEnumerable().Select(n =>
                        SelectTrustedModuleTemplateMarkup(n)
                    ).ToArray(),
                    HealthScripts = DbContext.HealthScripts.AsEnumerable().Select(n => SelectHealthScriptTemplateMarkup(n)).ToArray(),
                    AssetTemplates = DbContext.AssetTemplates.Include(n => n.FeatureGrants).ThenInclude(n => n.Feature)
                        .Include(n => n.Grants).ThenInclude(n => n.Permission)
                        .Include(n => n.PathTemplates)
                        .Include(n => n.RequiredFeature)
                        .Include(n => n.RequiredPermission).AsEnumerable().Select(n => SelectAssetTemplateMarkup(n)).ToArray()
                };


                return retVal;
            }
        }

        protected virtual AssetTemplateMarkup SelectAssetTemplateMarkup(TAssetTemplate assetTemplateInst)
        {
            return new AssetTemplateMarkup
            {
                Name = assetTemplateInst.Name,
                SystemKey = assetTemplateInst.SystemKey,
                RequiredFeature = assetTemplateInst.RequiredFeature.FeatureName,
                RequiredPermission = assetTemplateInst.RequiredPermission.PermissionName,
                Grants = assetTemplateInst.Grants.Select(n => n.Permission.PermissionName).ToArray(),
                FeatureGrants = assetTemplateInst.FeatureGrants.Select(n => n.Feature.FeatureName).ToArray(),
                PathTemplates = assetTemplateInst.PathTemplates.Select(n => n.PathTemplate).ToArray()
            };
        }

        protected virtual HealthScriptTemplateMarkup SelectHealthScriptTemplateMarkup(HealthScript healtScriptInst)
        {
            return new HealthScriptTemplateMarkup
            {
                HealthScriptName = healtScriptInst.HealthScriptName,
                Script = healtScriptInst.Script
            };
        }

        protected virtual TrustedModuleTemplateMarkup SelectTrustedModuleTemplateMarkup(TrustedFullAccessComponent trustedModuleInst)
        {
            return new TrustedModuleTemplateMarkup
            {
                FullQualifiedTypeName = trustedModuleInst.FullQualifiedTypeName,
                Description = trustedModuleInst.Description,
                TrustLevelConfig = trustedModuleInst.TrustLevelConfig,
                TargetQualifiedTypeName = trustedModuleInst.TargetQualifiedTypeName
            };
        }

        protected virtual DashboardWidgetLocaleTemplateMarkup SelectDashboardWidgetLocaleTemplateMarkup(TWidgetLocalization dashboardWidgetLocaleInst)
        {
            return new DashboardWidgetLocaleTemplateMarkup
            {
                LocaleName = dashboardWidgetLocaleInst.LocaleName,
                DisplayName = dashboardWidgetLocaleInst.DisplayName,
                SystemName = dashboardWidgetLocaleInst.Widget.SystemName,
                Template = dashboardWidgetLocaleInst.Template,
                TitleTemplate = dashboardWidgetLocaleInst.TitleTemplate
            };
        }

        protected virtual DashboardWidgetTemplateMarkup SelectDashboardWidgetTemplateMarkup(TWidget dashboardWidgetInst)
        {
            return new DashboardWidgetTemplateMarkup
            {
                SystemName = dashboardWidgetInst.SystemName, TitleTemplate = dashboardWidgetInst.TitleTemplate, Template = dashboardWidgetInst.Template,
                Area = dashboardWidgetInst.Area, CustomQueryString = dashboardWidgetInst.CustomQueryString, DisplayName = dashboardWidgetInst.DisplayName,
                DiagnosticsQueryName = dashboardWidgetInst.DiagnosticsQuery.DiagnosticsQueryName,
                Parameters = dashboardWidgetInst.Params.Select(p => SelectDashboardParamTemplateMarkup(p))
                    .ToArray()
            };
        }

        protected virtual DashboardParamTemplateMarkup SelectDashboardParamTemplateMarkup(TWidgetParam dasboardParamInst)
        {
            return new DashboardParamTemplateMarkup
            {
                InputConfig = dasboardParamInst.InputConfig, InputType = dasboardParamInst.InputType, ParameterName = dasboardParamInst.ParameterName
            };
        }

        protected virtual DiagnosticsQueryTemplateMarkup SelectDiagnosticsQueryTemplateMarkup(TQuery diagnosticsQueryInst)
        {
            return new DiagnosticsQueryTemplateMarkup
            {
                Permission = diagnosticsQueryInst.Permission.PermissionName, DbContext = diagnosticsQueryInst.DbContext, AutoReturn = diagnosticsQueryInst.AutoReturn,
                DiagnosticsQueryName = diagnosticsQueryInst.DiagnosticsQueryName, QueryText = diagnosticsQueryInst.QueryText,
                Parameters = diagnosticsQueryInst.Parameters.Select(p => SelectDiagnosticsQueryParameterTemplateMarkup(p)).ToArray()
            };
        }

        protected virtual DiagnosticsQueryParameterTemplateMarkup SelectDiagnosticsQueryParameterTemplateMarkup(TQueryParameter diagnosticsQueryParameterInst)
        {
            return new DiagnosticsQueryParameterTemplateMarkup
            {
                DefaultValue = diagnosticsQueryParameterInst.DefaultValue, Format = diagnosticsQueryParameterInst.Format, Optional = diagnosticsQueryParameterInst.Optional,
                ParameterName = diagnosticsQueryParameterInst.ParameterName, ParameterType = diagnosticsQueryParameterInst.ParameterType
            };
        }

        protected virtual SystemFeatureTemplateMarkup SelectSystemFeatureTemplateMarkup(Feature featureInst)
        {
            return new SystemFeatureTemplateMarkup
            {
                FeatureName = featureInst.FeatureName, Enabled = featureInst.Enabled, FeatureDescription = featureInst.FeatureDescription
            };
        }

        protected virtual TenantTemplateDefinitionMarkup SelectTenantTemplateDefinitionMarkup(TenantTemplate tenantTemplateInst)
        {
            return new TenantTemplateDefinitionMarkup
                { Name = tenantTemplateInst.Name, Description = tenantTemplateInst.Description, Markup = tenantTemplateInst.Markup };
        }

        protected virtual SettingTemplateMarkup SelectSettingTemplateMarkup(GlobalSetting settingInst)
        {
            return new SettingTemplateMarkup
                { Value = settingInst.SettingsValue, IsJsonSetting = settingInst.JsonSetting, ParamName = settingInst.SettingsKey };
        }

        protected virtual PlugInTemplateMarkup SelectPlugInTemplateMarkup(TWebPlugin plugInInst)
        {
            return new PlugInTemplateMarkup
            {
                AutoLoad = plugInInst.AutoLoad, Constructor = plugInInst.Constructor, UniqueName = plugInInst.UniqueName,
                GenericArguments = DbContext.GenericPluginParams.Where(c => c.WebPluginId == plugInInst.WebPluginId)
                    .Select(c => new PlugInGenericArgumentTemplateMarkup
                        { GenericTypeName = c.GenericTypeName, TypeExpression = c.TypeExpression }).ToArray()
            };
        }

        protected virtual ConstTemplateMarkup SelectConstTemplateMarkup(TWebPluginConstant constInst)
        {
            return new ConstTemplateMarkup { Name = constInst.Name, Value = constInst.Value };
        }

        protected virtual AuthenticationTypeClaimTemplateMarkup SelectAuthenticationTypeClaimTemplateMarkup(AuthenticationClaimMapping authenticationTypeClaimInst)
        {
            return new AuthenticationTypeClaimTemplateMarkup
            {
                AuthenticationTypeName = authenticationTypeClaimInst.AuthenticationType.AuthenticationTypeName,
                Condition = authenticationTypeClaimInst.Condition, IncomingClaimName = authenticationTypeClaimInst.IncomingClaimName,
                OutgoingClaimName = authenticationTypeClaimInst.OutgoingClaimName, OutgoingClaimValue = authenticationTypeClaimInst.OutgoingClaimValue,
                OutgoingIssuer = authenticationTypeClaimInst.OutgoingIssuer, OutgoingOriginalIssuer = authenticationTypeClaimInst.OutgoingOriginalIssuer,
                OutgoingValueType = authenticationTypeClaimInst.OutgoingValueType
            };
        }

        protected virtual AuthenticationTypeTemplateMarkup SelectAuthenticationTypeTemplateMarkup(AuthenticationType authenticationInst)
        {
            return new AuthenticationTypeTemplateMarkup
                { AuthenticationTypeName = authenticationInst.AuthenticationTypeName };
        }

        protected virtual GlobalRoleTemplateMarkup SelectGlobalRoleTemplateMarkup(TGlobalRole globalRoleInst)
        {
            return new GlobalRoleTemplateMarkup
            {
                RoleName = globalRoleInst.RoleName,
                RoleDescription = globalRoleInst.RoleDescription,
                Permissions = globalRoleInst.RolePermissions.Select(p => p.Permission.PermissionName).ToArray()
            };
        }

        protected virtual PermissionTemplateMarkup SelectPermissionTemplateMarkup(TPermission permissionInst)
        {
            return new PermissionTemplateMarkup
                { Description = permissionInst.Description, Global = true, Name = permissionInst.PermissionName };
        }

        private NavigationMenuTemplateMarkup[] GetSortedNav()
        {
            var allNav = DbContext.Navigation.ToList();
            var sortedNav = new List<TNavigationMenu>();
            var lastCt = 0;
            while (allNav.Count != 0 && lastCt != allNav.Count)
            {
                lastCt = allNav.Count;
                var tmp = allNav.Where(n => n.ParentId == null || sortedNav.Any(p => p.NavigationMenuId == n.ParentId))
                    .ToArray();
                sortedNav.AddRange(tmp);
                tmp.ForEach(n => allNav.Remove(n));
            }

            return sortedNav.Select(n => SelectNavigationMenuTemplateMarkup(n)).ToArray();
        }

        protected virtual NavigationMenuTemplateMarkup SelectNavigationMenuTemplateMarkup(TNavigationMenu navigationMenuInst)
        {
            return new NavigationMenuTemplateMarkup
            {
                FeatureName = navigationMenuInst.Feature?.FeatureName, PermissionName = navigationMenuInst.EntryPoint?.PermissionName, RefTag = navigationMenuInst.RefTag,
                DisplayName = navigationMenuInst.DisplayName, ParentRef = navigationMenuInst.Parent?.RefTag, SortOrder = navigationMenuInst.SortOrder,
                SpanClass = navigationMenuInst.SpanClass, Url = navigationMenuInst.Url,
                IsPublic = navigationMenuInst.IsPublic
            };
        }

        private void CompareNavigation(NavigationMenuTemplateMarkup[] sysNavigation, NavigationMenuTemplateMarkup[] upSysNavigation)
        {
            var keyNames = new string[] { "RefTag" };
            var entityName = "Navigation";
            var keyExp = new Dictionary<string, string>
            {
            };
            var groups = (from t in sysNavigation select t.RefTag).Union(from t in upSysNavigation select t.RefTag).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysNavigation on c equals a1.RefTag into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysNavigation on c equals a2.RefTag into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.RefTag}" },
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.RefTag));
                    change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName, multiline:true));
                    change.Details.Add(MakeDetail("SpanClass", c.New.SpanClass));
                    change.Details.Add(MakeDetail("Url", c.New.Url));
                    change.Details.Add(MakeDetail("IsPublic", c.New.IsPublic.ToString(), "Entity.IsPublic=(NewValueRaw==\"True\")"));
                    change.Details.Add(MakeDetail("SortOrder", c.New.SortOrder.ToString()));
                    change.Details.Add(MakeDetail("Feature", c.New.FeatureName, MakeLinqAssign<TContext>("Feature", "Features", "FeatureName")));
                    change.Details.Add(MakeDetail("EntryPoint", c.New.PermissionName, MakeLinqAssign<TContext>("EntryPoint", "Permissions", "PermissionName")));
                    change.Details.Add(MakeDetail("Parent", c.New.ParentRef, MakeLinqAssign<TContext>("Parent", "Navigation", keyNames[0])));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.RefTag}" },
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.DisplayName != c.Original.DisplayName && !string.IsNullOrWhiteSpace(c.New.DisplayName) && !string.IsNullOrWhiteSpace(c.Original.DisplayName)) || (string.IsNullOrWhiteSpace(c.New.DisplayName) != string.IsNullOrWhiteSpace(c.Original.DisplayName)))
                    {
                        change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName, currentValue: c.Original.DisplayName, multiline: true));
                    }

                    if ((c.New.SpanClass != c.Original.SpanClass && !string.IsNullOrWhiteSpace(c.New.SpanClass) && !string.IsNullOrWhiteSpace(c.Original.SpanClass)) || (string.IsNullOrWhiteSpace(c.New.SpanClass) != string.IsNullOrWhiteSpace(c.Original.SpanClass)))
                    {
                        change.Details.Add(MakeDetail("SpanClass", c.New.SpanClass));
                    }

                    if ((c.New.Url != c.Original.Url && !string.IsNullOrWhiteSpace(c.New.Url) && !string.IsNullOrWhiteSpace(c.Original.Url)) || (string.IsNullOrWhiteSpace(c.New.Url) != string.IsNullOrWhiteSpace(c.Original.Url)))
                    {
                        change.Details.Add(MakeDetail("Url", c.New.Url, currentValue:c.Original.Url));
                    }

                    if (c.New.IsPublic!= c.Original.IsPublic)
                    {
                        change.Details.Add(MakeDetail("IsPublic", c.New.IsPublic.ToString(), currentValue:c.Original.IsPublic.ToString(), valueExpression:"Entity.IsPublic=(NewValueRaw==\"True\")"));
                    }

                    if (c.New.SortOrder != c.Original.SortOrder)
                    {
                        change.Details.Add(MakeDetail("SortOrder", c.New.SortOrder.ToString()));
                    }

                    if ((c.New.FeatureName != c.Original.FeatureName && !string.IsNullOrWhiteSpace(c.New.FeatureName) && !string.IsNullOrWhiteSpace(c.Original.FeatureName)) || (string.IsNullOrWhiteSpace(c.New.FeatureName) != string.IsNullOrWhiteSpace(c.Original.FeatureName)))
                    {
                        change.Details.Add(MakeDetail("Feature", c.New.FeatureName, MakeLinqAssign<TContext>("Feature", "Features", "FeatureName"), c.Original.FeatureName));
                    }

                    if ((c.New.PermissionName != c.Original.PermissionName && !string.IsNullOrWhiteSpace(c.New.PermissionName) && !string.IsNullOrWhiteSpace(c.Original.PermissionName)) || (string.IsNullOrWhiteSpace(c.New.PermissionName) != string.IsNullOrWhiteSpace(c.Original.PermissionName)))
                    {
                        change.Details.Add(MakeDetail("EntryPoint", c.New.PermissionName, MakeLinqAssign<TContext>("EntryPoint", "Permissions", "PermissionName"), c.Original.PermissionName));
                    }

                    if ((c.New.ParentRef != c.Original.ParentRef && !string.IsNullOrWhiteSpace(c.New.ParentRef) && !string.IsNullOrWhiteSpace(c.Original.ParentRef))  || (string.IsNullOrWhiteSpace(c.New.ParentRef) != string.IsNullOrWhiteSpace(c.Original.ParentRef)))
                    {
                        change.Details.Add(MakeDetail("Parent", c.New.ParentRef, MakeLinqAssign<TContext>("Parent", "Navigation", keyNames[0]), c.Original.ParentRef));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessNavigationChange(change, c.New, c.Original);
                }
            }
        }

        private void CompareHealthScripts(HealthScriptTemplateMarkup[] sysHealthScripts,
            HealthScriptTemplateMarkup[] upSysHealthScripts)
        {
            var keyNames = new string[] { "HealthScriptName" };
            var entityName = "HealthScripts";

            var keyExp = new Dictionary<string, string>
            {
            };

            var groups = (from t in sysHealthScripts select t.HealthScriptName.ToLower()).Union(from t in upSysHealthScripts select t.HealthScriptName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in sysHealthScripts on c equals  a1.HealthScriptName.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in upSysHealthScripts on c equals a2.HealthScriptName.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Name = c, Original = na1, New = na2 }).ToArray();

            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], c.Original.HealthScriptName}
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.HealthScriptName));
                    change.Details.Add(MakeDetail("Script", c.New.Script, multiline:true));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], c.Original.HealthScriptName}
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.Script != c.Original.Script && !string.IsNullOrWhiteSpace(c.New.Script) && !string.IsNullOrWhiteSpace(c.Original.Script)) || (string.IsNullOrWhiteSpace(c.New.Script) != string.IsNullOrWhiteSpace(c.Original.Script)))
                    {
                        change.Details.Add(MakeDetail("Script", c.New.Script, currentValue: c.Original.Script, multiline: true));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessHealthScriptChange(change, c.New, c.Original);
                }
            }
        }

        private void CompareDashboardWidgetLocales(DashboardWidgetLocaleTemplateMarkup[] sysDashboardWidgetLocales,
            DashboardWidgetLocaleTemplateMarkup[] upSysDashboardWidgetLocales)
        {
            var keyNames = new string[] { "Widget", "LocaleName" };
            var entityName = "WidgetLocales";
            
            var keyExp = new Dictionary<string, string>
            {
                {keyNames[0], MakeLinqQuery<TContext>("Widgets", "SystemName", filterValueVariable: "Value")}
            };

            var groups = (from t in sysDashboardWidgetLocales select new {SystemName= t.SystemName.ToLower(), LocaleName=t.LocaleName.ToLower()}).Union(from t in upSysDashboardWidgetLocales select new { SystemName = t.SystemName.ToLower(), LocaleName = t.LocaleName.ToLower() }).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in sysDashboardWidgetLocales on c equals new {SystemName = a1.SystemName.ToLower(), LocaleName = a1.LocaleName.ToLower()} into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in upSysDashboardWidgetLocales on c equals new { SystemName = a2.SystemName.ToLower(), LocaleName = a2.LocaleName.ToLower() } into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], c.Original.SystemName},
                        {keyNames[1], c.Original.LocaleName}
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail("LocaleName", c.New.LocaleName));
                    change.Details.Add(MakeDetail("Widget", c.New.SystemName, MakeLinqAssign<TContext>("Widget", "Widgets", "SystemName")));
                    change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName, multiline: true));
                    change.Details.Add(MakeDetail("TitleTemplate", c.New.TitleTemplate, multiline: true));
                    change.Details.Add(MakeDetail("Template", c.New.Template, multiline: true));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], c.Original.SystemName},
                            {keyNames[1], c.Original.LocaleName}
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.DisplayName != c.Original.DisplayName && !string.IsNullOrWhiteSpace(c.New.DisplayName) && !string.IsNullOrWhiteSpace(c.Original.DisplayName)) || (string.IsNullOrWhiteSpace(c.New.DisplayName) != string.IsNullOrWhiteSpace(c.Original.DisplayName)))
                    {
                        change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName, currentValue: c.Original.DisplayName, multiline: true));
                    }

                    if ((c.New.TitleTemplate != c.Original.TitleTemplate && !string.IsNullOrWhiteSpace(c.New.TitleTemplate) && !string.IsNullOrWhiteSpace(c.Original.TitleTemplate)) || (string.IsNullOrWhiteSpace(c.New.TitleTemplate) != string.IsNullOrWhiteSpace(c.Original.TitleTemplate)))
                    {
                        change.Details.Add(MakeDetail("TitleTemplate", c.New.TitleTemplate, currentValue: c.Original.TitleTemplate, multiline: true));
                    }

                    if ((c.New.Template != c.Original.Template && !string.IsNullOrWhiteSpace(c.New.Template) && !string.IsNullOrWhiteSpace(c.Original.Template)) || (string.IsNullOrWhiteSpace(c.New.Template) != string.IsNullOrWhiteSpace(c.Original.Template)))
                    {
                        change.Details.Add(MakeDetail("Template", c.New.Template, currentValue: c.Original.Template, multiline: true));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessDashboardWidgetLocaleChange(change, c.New, c.Original);
                }
            }
        }

        private void CompareDashboardWidgets(DashboardWidgetTemplateMarkup[] sysDashboardWidgets, DashboardWidgetTemplateMarkup[] upSysDashboardWidgets)
        {
            var keyNames = new string[] { "SystemName" };
            var entityName = "Widgets";
            var keyExp = new Dictionary<string, string>
            {
            };
            var groups = (from t in sysDashboardWidgets select t.SystemName.ToLower()).Union(from t in upSysDashboardWidgets select t.SystemName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysDashboardWidgets on c equals a1.SystemName.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysDashboardWidgets on c equals a2.SystemName.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], c.Original.SystemName}
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.SystemName));
                    change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName, multiline: true));
                    change.Details.Add(MakeDetail("Area", c.New.Area));
                    change.Details.Add(MakeDetail("CustomQueryString", c.New.CustomQueryString));
                    change.Details.Add(MakeDetail("TitleTemplate", c.New.TitleTemplate, multiline: true));
                    change.Details.Add(MakeDetail("Template", c.New.Template, multiline: true));
                    change.Details.Add(MakeDetail("DiagnosticsQuery", c.New.DiagnosticsQueryName, MakeLinqAssign<TContext>("DiagnosticsQuery", "DiagnosticsQueries", "DiagnosticsQueryName")));
                    RegisterChange(change);
                    RegisterWidgetParameters(c.New.SystemName, c.New.Parameters);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], c.Original.SystemName}
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.DisplayName != c.Original.DisplayName && !string.IsNullOrWhiteSpace(c.New.DisplayName) && !string.IsNullOrWhiteSpace(c.Original.DisplayName)) || (string.IsNullOrWhiteSpace(c.New.DisplayName) != string.IsNullOrWhiteSpace(c.Original.DisplayName)))
                    {
                        change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName, currentValue: c.Original.DisplayName, multiline: true));
                    }

                    if ((c.New.Area != c.Original.Area && !string.IsNullOrWhiteSpace(c.New.Area) && !string.IsNullOrWhiteSpace(c.Original.Area)) || (string.IsNullOrWhiteSpace(c.New.Area) != string.IsNullOrWhiteSpace(c.Original.Area)))
                    {
                        change.Details.Add(MakeDetail("Area", c.New.Area, currentValue: c.Original.Area));
                    }

                    if ((c.New.CustomQueryString != c.Original.CustomQueryString && !string.IsNullOrWhiteSpace(c.New.CustomQueryString) && !string.IsNullOrWhiteSpace(c.Original.CustomQueryString)) || (string.IsNullOrWhiteSpace(c.New.CustomQueryString) != string.IsNullOrWhiteSpace(c.Original.CustomQueryString)))
                    {
                        change.Details.Add(MakeDetail("CustomQueryString", c.New.CustomQueryString, currentValue: c.Original.CustomQueryString));
                    }

                    if ((c.New.TitleTemplate != c.Original.TitleTemplate && !string.IsNullOrWhiteSpace(c.New.TitleTemplate) && !string.IsNullOrWhiteSpace(c.Original.TitleTemplate)) || (string.IsNullOrWhiteSpace(c.New.TitleTemplate) != string.IsNullOrWhiteSpace(c.Original.TitleTemplate)))
                    {
                        change.Details.Add(MakeDetail("TitleTemplate", c.New.TitleTemplate, currentValue: c.Original.TitleTemplate, multiline: true));
                    }

                    if ((c.New.Template != c.Original.Template && !string.IsNullOrWhiteSpace(c.New.Template) && !string.IsNullOrWhiteSpace(c.Original.Template)) || (string.IsNullOrWhiteSpace(c.New.Template) != string.IsNullOrWhiteSpace(c.Original.Template)))
                    {
                        change.Details.Add(MakeDetail("Template", c.New.Template, currentValue: c.Original.Template, multiline: true));
                    }

                    if ((c.New.DiagnosticsQueryName != c.Original.DiagnosticsQueryName && !string.IsNullOrWhiteSpace(c.New.DiagnosticsQueryName) && !string.IsNullOrWhiteSpace(c.Original.DiagnosticsQueryName)) || (string.IsNullOrWhiteSpace(c.New.DiagnosticsQueryName) != string.IsNullOrWhiteSpace(c.Original.DiagnosticsQueryName)))
                    {
                        change.Details.Add(MakeDetail("DiagnosticsQuery", c.New.DiagnosticsQueryName,
                            MakeLinqAssign<TContext>("DiagnosticsQuery", "DiagnosticsQueries", "DiagnosticsQueryName"), c.Original.DiagnosticsQueryName));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }

                    RegisterWidgetParameters(c.New.SystemName, c.New.Parameters, c.Original.Parameters);
                }

                if (change != null)
                {
                    PostProcessDashboardWidgetChange(change, c.New, c.Original);
                }
            }
        }

        private void RegisterWidgetParameters(string dashboardName, DashboardParamTemplateMarkup[] parameters, DashboardParamTemplateMarkup[] originalParameters = null)
        {
            originalParameters ??= Array.Empty<DashboardParamTemplateMarkup>();
            var keyNames = new string[] { "ParameterName", "Parent" };
            var entityName = "WidgetParams";
            var keyExp = new Dictionary<string, string>
            {
                {keyNames[1], MakeLinqQuery<TContext>("Widgets", "SystemName", filterValueVariable: "Value")}
            };
            var groups = (from t in originalParameters select t.ParameterName.ToLower()).Union(from t in parameters select t.ParameterName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in originalParameters on c equals a1.ParameterName.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in parameters on c equals a2.ParameterName.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.ParameterName}" },
                        {keyNames[1], dashboardName}
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.ParameterName));
                    change.Details.Add(MakeDetail(keyNames[1], dashboardName, MakeLinqAssign<TContext>(keyNames[1], "Widgets", "SystemName")));
                    change.Details.Add(MakeDetail("InputType", c.New.InputType.ToString()));
                    change.Details.Add(MakeDetail("InputConfig", c.New.InputConfig));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.ParameterName}" },
                            {keyNames[1], dashboardName}
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if (c.New.InputType != c.Original.InputType)
                    {
                        change.Details.Add(MakeDetail("InputType", c.New.InputType.ToString(), currentValue:c.Original.InputType.ToString()));
                    }

                    if ((c.New.InputConfig != c.Original.InputConfig && !string.IsNullOrWhiteSpace(c.New.InputConfig) && !string.IsNullOrWhiteSpace(c.Original.InputConfig)) || (string.IsNullOrWhiteSpace(c.New.InputConfig) != string.IsNullOrWhiteSpace(c.Original.InputConfig)))
                    {
                        change.Details.Add(MakeDetail("InputConfig", c.New.InputConfig, currentValue:c.Original.InputConfig));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessWidgetParameterChange(dashboardName, change, c.New, c.Original);
                }
            }
        }

        private void CompareAssetTemplates(AssetTemplateMarkup[] sysAssetTemplates,
            AssetTemplateMarkup[] upSysAssetTemplates)
        {
            var keyNames = new string[] { "SystemKey" };
            var entityName = "AssetTemplates";
            var keyExp = new Dictionary<string, string>
            {
            };

            var groups = (from t in sysAssetTemplates select t.SystemKey.ToLower()).Union(from t in upSysAssetTemplates select t.SystemKey.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in sysAssetTemplates on c equals a1.SystemKey.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in upSysAssetTemplates on c equals a2.SystemKey.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Name = c, Original = na1, New = na2 }).ToArray();

            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.SystemKey}" },
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.SystemKey));
                    change.Details.Add(MakeDetail("Name", c.New.Name));
                    change.Details.Add(MakeDetail("RequiredPermission", c.New.RequiredPermission, MakeLinqAssign<TContext>("RequiredPermission", "Permissions", "PermissionName", "n.TenantId==null")));
                    change.Details.Add(MakeDetail("RequiredFeature", c.New.RequiredFeature, MakeLinqAssign<TContext>("RequiredFeature", "Features", "FeatureName")));
                    RegisterChange(change);
                    RegisterAssetPermissions(c.New.SystemKey, c.New.Grants);
                    RegisterAssetFeatures(c.New.SystemKey, c.New.FeatureGrants);
                    RegisterAssetPathFilters(c.New.SystemKey, c.New.PathTemplates);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.SystemKey}" },
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.Name != c.Original.Name && !string.IsNullOrWhiteSpace(c.New.Name) && !string.IsNullOrWhiteSpace(c.Original.Name)) || (string.IsNullOrWhiteSpace(c.New.Name) != string.IsNullOrWhiteSpace(c.Original.Name)))
                    {
                        change.Details.Add(MakeDetail("Name", c.New.Name, currentValue: c.Original.Name));
                    }

                    if ((c.New.RequiredPermission != c.Original.RequiredPermission && !string.IsNullOrWhiteSpace(c.New.RequiredPermission) && !string.IsNullOrWhiteSpace(c.Original.RequiredPermission)) || (string.IsNullOrWhiteSpace(c.New.RequiredPermission) != string.IsNullOrWhiteSpace(c.Original.RequiredPermission)))
                    {
                        change.Details.Add(MakeDetail("RequiredPermission", c.New.RequiredPermission, MakeLinqAssign<TContext>("RequiredPermission", "Permissions", "PermissionName", "n.TenantId==null"), c.Original.RequiredPermission)); }

                    if ((c.New.RequiredFeature != c.Original.RequiredFeature && !string.IsNullOrWhiteSpace(c.New.RequiredFeature) && !string.IsNullOrWhiteSpace(c.Original.RequiredFeature)) || (string.IsNullOrWhiteSpace(c.New.RequiredFeature) != string.IsNullOrWhiteSpace(c.Original.RequiredFeature)))
                    {
                        change.Details.Add(MakeDetail("RequiredFeature", c.New.RequiredFeature, MakeLinqAssign<TContext>("RequiredFeature", "Features", "FeatureName"), c.Original.RequiredFeature));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }

                    RegisterAssetPermissions(c.New.SystemKey, c.New.Grants, c.Original.Grants);
                    RegisterAssetFeatures(c.New.SystemKey, c.New.FeatureGrants, c.Original.FeatureGrants);
                    RegisterAssetPathFilters(c.New.SystemKey, c.New.PathTemplates, c.Original.PathTemplates);
                }

                if (change != null)
                {
                    PostProcessAssetTemplateChange(change, c.New, c.Original);
                }
            }
        }

        private void RegisterAssetPermissions(string systemKey, string[] grants, string[] originalGrants = null)
        {
            originalGrants ??= Array.Empty<string>();
            var keyNames = new string[] { "Template", "Permission" };
            var entityName = "AssetTemplateGrants";
            var keyExp = new Dictionary<string, string>
            {
                { keyNames[0], MakeLinqQuery<TContext>("AssetTemplates", "SystemKey", filterValueVariable: "Value") },
                { keyNames[1], MakeLinqQuery<TContext>("Permissions", "PermissionName", filterValueVariable: "Value", additionalWhere: "n.TenantId==null") }
            };
            var groups = (from t in originalGrants select t.ToLower()).Union(from t in grants select t.ToLower())
                .Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in originalGrants on c equals a1.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in grants on c equals a2.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], systemKey },
                            { keyNames[1], c.Original }
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], systemKey, MakeLinqAssign<TContext>(keyNames[1], "AssetTemplates", "SystemKey")));
                    change.Details.Add(MakeDetail(keyNames[1], c.New, MakeLinqAssign<TContext>(keyNames[1], "Permissions", "PermissionName", "n.TenantId==null")));
                    RegisterChange(change);
                }

                if (change != null)
                {
                    PostProcessAssetPermissionChange(systemKey, change, c.New, c.Original);
                }
            }
        }

        private void RegisterAssetFeatures(string systemKey, string[] grants, string[] originalGrants = null)
        {
            originalGrants ??= Array.Empty<string>();
            var keyNames = new string[] { "Template", "Feature" };
            var entityName = "AssetTemplateFeatures";
            var keyExp = new Dictionary<string, string>
            {
                {keyNames[0], MakeLinqQuery<TContext>("AssetTemplates", "SystemKey", filterValueVariable: "Value")},
                { keyNames[1], MakeLinqQuery<TContext>("Features", "FeatureName", filterValueVariable: "Value") }
            };
            var groups = (from t in originalGrants select t.ToLower()).Union(from t in grants select t.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in originalGrants on c equals a1.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in grants on c equals a2.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], systemKey },
                            { keyNames[1], c.Original }
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], systemKey, MakeLinqAssign<TContext>(keyNames[1], "AssetTemplates", "SystemKey")));
                    change.Details.Add(MakeDetail(keyNames[1], c.New, MakeLinqAssign<TContext>(keyNames[1], "Permissions", "PermissionName", "n.TenantId==null")));
                    RegisterChange(change);
                }

                if (change != null)
                {
                    PostProcessAssetFeatureChange(systemKey, change, c.New, c.Original);
                }
            }
        }

        private void RegisterAssetPathFilters(string systemKey, string[] filters, string[] originalFilters = null)
        {
            originalFilters ??= Array.Empty<string>();
            var keyNames = new string[] { "Template", "PathTemplate" };
            var entityName = "AssetTemplateFeatures";
            var keyExp = new Dictionary<string, string>
            {
                {keyNames[0], MakeLinqQuery<TContext>("AssetTemplates", "SystemKey", filterValueVariable: "Value")}
            };
            var groups = (from t in originalFilters select t.ToLower()).Union(from t in filters select t.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in originalFilters on c equals a1.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in filters on c equals a2.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], systemKey },
                            { keyNames[1], c.Original }
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], systemKey, MakeLinqAssign<TContext>(keyNames[1], "AssetTemplates", "SystemKey")));
                    change.Details.Add(MakeDetail(keyNames[1], c.New));
                    RegisterChange(change);
                }

                if (change != null)
                {
                    PostProcessAssetPathFilterChange(systemKey, change, c.New, c.Original);
                }
            }
        }

        private void CompareDiagnosticsQueries(DiagnosticsQueryTemplateMarkup[] sysDiagnosticsQueries, DiagnosticsQueryTemplateMarkup[] upSysDiagnosticsQueries)
        {
            var keyNames = new string[] { "DiagnosticsQueryName"};
            var entityName = "DiagnosticsQueries";
            var keyExp = new Dictionary<string, string>
            {
            };
            var groups = (from t in sysDiagnosticsQueries select t.DiagnosticsQueryName.ToLower()).Union(from t in upSysDiagnosticsQueries select t.DiagnosticsQueryName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysDiagnosticsQueries on c equals a1.DiagnosticsQueryName.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysDiagnosticsQueries on c equals a2.DiagnosticsQueryName.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.DiagnosticsQueryName}" },
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.DiagnosticsQueryName ));
                    change.Details.Add(MakeDetail("DbContext", c.New.DbContext));
                    change.Details.Add(MakeDetail("QueryText", c.New.QueryText, multiline: true));
                    change.Details.Add(MakeDetail("AutoReturn", c.New.AutoReturn.ToString(), "Entity.AutoReturn=(NewValueRaw==\"True\")"));
                    change.Details.Add(MakeDetail("Permission", c.New.Permission, MakeLinqAssign<TContext>("Permission", "Permissions", "PermissionName", "n.TenantId==null")));
                    RegisterChange(change);
                    RegisterQueryParameters(c.New.DiagnosticsQueryName, c.New.Parameters);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.DiagnosticsQueryName}" },
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.DbContext != c.Original.DbContext && !string.IsNullOrWhiteSpace(c.New.DbContext) && !string.IsNullOrWhiteSpace(c.Original.DbContext)) || (string.IsNullOrWhiteSpace(c.New.DbContext) != string.IsNullOrWhiteSpace(c.Original.DbContext)))
                    {
                        change.Details.Add(MakeDetail("DbContext", c.New.DbContext, currentValue: c.Original.DbContext));
                    }

                    if ((c.New.QueryText != c.Original.QueryText && !string.IsNullOrWhiteSpace(c.New.QueryText) && !string.IsNullOrWhiteSpace(c.Original.QueryText)) || (string.IsNullOrWhiteSpace(c.New.QueryText) != string.IsNullOrWhiteSpace(c.Original.QueryText)))
                    {
                        change.Details.Add(MakeDetail("QueryText", c.New.QueryText, currentValue: c.Original.QueryText, multiline:true));
                    }

                    if (c.New.AutoReturn!= c.Original.AutoReturn)
                    {
                        change.Details.Add(MakeDetail("AutoReturn", c.New.AutoReturn.ToString(), "Entity.AutoReturn=(NewValueRaw==\"True\")", c.Original.AutoReturn.ToString()));
                    }

                    if ((c.New.Permission != c.Original.Permission && !string.IsNullOrWhiteSpace(c.New.Permission) && !string.IsNullOrWhiteSpace(c.Original.Permission)) || (string.IsNullOrWhiteSpace(c.New.Permission) != string.IsNullOrWhiteSpace(c.Original.Permission)))
                    {
                        change.Details.Add(MakeDetail("Permission", c.New.Permission, MakeLinqAssign<TContext>("Permission", "Permissions", "PermissionName", "n.TenantId==null"), c.Original.Permission));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }

                    RegisterQueryParameters(c.New.DiagnosticsQueryName, c.New.Parameters, c.Original.Parameters);
                }

                if (change != null)
                {
                    PostProcessDiagnosticsQueryChange(change, c.New, c.Original);
                }
            }
        }

        private void RegisterQueryParameters(string diagnosticsQueryName, DiagnosticsQueryParameterTemplateMarkup[] parameters, DiagnosticsQueryParameterTemplateMarkup[] originalParameters = null)
        {
            originalParameters ??= Array.Empty<DiagnosticsQueryParameterTemplateMarkup>();
            var keyNames = new string[] { "ParameterName", "DiagnosticsQuery"};
            var entityName = "DiagnosticsQueryParameters";
            var keyExp = new Dictionary<string, string>
            {
                {keyNames[1], MakeLinqQuery<TContext>("DiagnosticsQueries", "DiagnosticsQueryName", filterValueVariable: "Value")}
            };
            var groups = (from t in originalParameters select t.ParameterName.ToLower()).Union(from t in parameters select t.ParameterName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in originalParameters on c equals a1.ParameterName.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in parameters on c equals a2.ParameterName.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.ParameterName}" },
                        {keyNames[1], diagnosticsQueryName}
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.ParameterName));
                    change.Details.Add(MakeDetail(keyNames[1], diagnosticsQueryName, MakeLinqAssign<TContext>(keyNames[1], "DiagnosticsQueries", "DiagnosticsQueryName")));
                    change.Details.Add(MakeDetail("ParameterType", c.New.ParameterType.ToString()));
                    change.Details.Add(MakeDetail("Format", c.New.Format));
                    change.Details.Add(MakeDetail("DefaultValue", c.New.DefaultValue));
                    change.Details.Add(MakeDetail("Optional", c.New.Optional.ToString()));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.ParameterName}" },
                            {keyNames[1], diagnosticsQueryName}
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if (c.New.ParameterType != c.Original.ParameterType)
                    {
                        change.Details.Add(MakeDetail("ParameterType", c.New.ParameterType.ToString(), currentValue: c.Original.ParameterType.ToString()));
                    }

                    if (c.New.Optional != c.Original.Optional)
                    {
                        change.Details.Add(MakeDetail("Optional", c.New.Optional.ToString(), currentValue: c.Original.Optional.ToString()));
                    }

                    if ((c.New.Format != c.Original.Format && !string.IsNullOrWhiteSpace(c.New.Format) && !string.IsNullOrWhiteSpace(c.Original.Format)) || (string.IsNullOrWhiteSpace(c.New.Format) != string.IsNullOrWhiteSpace(c.Original.Format)))
                    {
                        change.Details.Add(MakeDetail("Format", c.New.Format, currentValue: c.Original.Format));
                    }

                    if ((c.New.DefaultValue != c.Original.DefaultValue && !string.IsNullOrWhiteSpace(c.New.DefaultValue) && !string.IsNullOrWhiteSpace(c.Original.DefaultValue)) || (string.IsNullOrWhiteSpace(c.New.DefaultValue) != string.IsNullOrWhiteSpace(c.Original.DefaultValue)))
                    {
                        change.Details.Add(MakeDetail("DefaultValue", c.New.DefaultValue, currentValue: c.Original.DefaultValue));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessQueryParameterChange(diagnosticsQueryName, change, c.New, c.Original);
                }
            }
        }

        private void CompareTenantTemplates(TenantTemplateDefinitionMarkup[] sysTenantTemplates, TenantTemplateDefinitionMarkup[] upSysTenantTemplates)
        {
            var keyNames = new string[] { "Name" };
            var entityName = "TenantTemplates";
            var keyExp = new Dictionary<string, string>
            {
            };
            var groups = (from t in sysTenantTemplates select t.Name.ToLower()).Union(from t in upSysTenantTemplates select t.Name.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysTenantTemplates on c equals a1.Name.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysTenantTemplates on c equals a2.Name.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.Name}" }
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.Name));
                    change.Details.Add(MakeDetail("Description", c.New.Description, multiline: true));
                    change.Details.Add(MakeDetail("Markup", c.New.Markup, multiline: true));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.Name}" }
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.Description != c.Original.Description && !string.IsNullOrWhiteSpace(c.New.Description) && !string.IsNullOrWhiteSpace(c.Original.Description)) || (string.IsNullOrWhiteSpace(c.New.Description) != string.IsNullOrWhiteSpace(c.Original.Description)))
                    {
                        change.Details.Add(MakeDetail("Description", c.New.Description, currentValue: c.Original.Description, multiline: true));
                    }

                    if ((c.New.Markup != c.Original.Markup && !string.IsNullOrWhiteSpace(c.New.Markup) && !string.IsNullOrWhiteSpace(c.Original.Markup)) || (string.IsNullOrWhiteSpace(c.New.Markup) != string.IsNullOrWhiteSpace(c.Original.Markup)))
                    {
                        change.Details.Add(MakeDetail("Markup", c.New.Markup, currentValue: c.Original.Markup, multiline: true));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessTenantTemplateChange(change, c.New, c.Original);
                }
            }
        }

        private void CompareTrustedModules(TrustedModuleTemplateMarkup[] sysTrusts, TrustedModuleTemplateMarkup[] upTrusts)
        {
            var keyNames = new string[] { "FullQualifiedTypeName", "TargetQualifiedTypeName" };
            var entityName = "TrustedFullAccessComponents";
            var keyExp = new Dictionary<string, string>
            {
            };
            var groups = (from t in sysTrusts select new {FullQualifiedTypeName=t.FullQualifiedTypeName.ToLower(), TargetQualifiedTypeName = t.TargetQualifiedTypeName.ToLower()}).Union(from t in upTrusts select new {FullQualifiedTypeName=t.FullQualifiedTypeName.ToLower(), TargetQualifiedTypeName = t.TargetQualifiedTypeName.ToLower()}).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysTrusts on c equals new { FullQualifiedTypeName = a1.FullQualifiedTypeName.ToLower(), TargetQualifiedTypeName = a1.TargetQualifiedTypeName.ToLower() } into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upTrusts on c equals new { FullQualifiedTypeName = a2.FullQualifiedTypeName.ToLower(), TargetQualifiedTypeName = a2.TargetQualifiedTypeName.ToLower() } into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.FullQualifiedTypeName}" },
                        {keyNames[1], $"{c.Original.TargetQualifiedTypeName}"}
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.FullQualifiedTypeName));
                    change.Details.Add(MakeDetail("Description", c.New.Description, multiline: true));
                    change.Details.Add(MakeDetail("TrustLevelConfig", c.New.TrustLevelConfig));
                    change.Details.Add(MakeDetail(keyNames[1], c.New.TargetQualifiedTypeName));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.FullQualifiedTypeName}" },
                            {keyNames[1], $"{c.Original.TargetQualifiedTypeName}"}
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.Description != c.Original.Description && !string.IsNullOrWhiteSpace(c.New.Description) && !string.IsNullOrWhiteSpace(c.Original.Description)) || (string.IsNullOrWhiteSpace(c.New.Description) != string.IsNullOrWhiteSpace(c.Original.Description)))
                    {
                        change.Details.Add(MakeDetail("Description", c.New.Description, currentValue: c.Original.Description, multiline: true));
                    }

                    if (c.New.TrustLevelConfig != c.Original.TrustLevelConfig)
                    {
                        change.Details.Add(MakeDetail("TrustLevelConfig", c.New.TrustLevelConfig, currentValue:c.Original.TrustLevelConfig));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessTrustedModuleChange(change, c.New, c.Original);
                }
            }
        }

        private void CompareFeatures(SystemFeatureTemplateMarkup[] sysFeatures, SystemFeatureTemplateMarkup[] upSysFeatures)
        {
            var keyNames = new string[] { "FeatureName" };
            var entityName = "Features";
            var keyExp = new Dictionary<string, string>
            {
            };
            var groups = (from t in sysFeatures select t.FeatureName.ToLower()).Union(from t in upSysFeatures select t.FeatureName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysFeatures on c equals a1.FeatureName.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysFeatures on c equals a2.FeatureName.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.FeatureName}" }
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.FeatureName));
                    change.Details.Add(MakeDetail("FeatureDescription", c.New.FeatureDescription, multiline: true));
                    change.Details.Add(MakeDetail("Enabled", c.New.Enabled.ToString(), "Entity.Enabled=(NewValueRaw==\"True\")"));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.FeatureName}" }
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.FeatureDescription != c.Original.FeatureDescription && !string.IsNullOrWhiteSpace(c.New.FeatureDescription) && !string.IsNullOrWhiteSpace(c.Original.FeatureDescription)) || (string.IsNullOrWhiteSpace(c.New.FeatureDescription) != string.IsNullOrWhiteSpace(c.Original.FeatureDescription)))
                    {
                        change.Details.Add(MakeDetail("FeatureDescription", c.New.FeatureDescription, currentValue: c.Original.FeatureDescription, multiline: true));
                    }

                    if (c.New.Enabled != c.Original.Enabled)
                    {
                        change.Details.Add(MakeDetail("Enabled", c.New.Enabled.ToString(), "Entity.Enabled=(NewValueRaw==\"True\")", c.Original.Enabled.ToString()));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessFeatureChange(change, c.New, c.Original);
                }
            }
        }

        private void CompareGlobalSettings(SettingTemplateMarkup[] sysSettings, SettingTemplateMarkup[] upSysSettings)
        {
            var keyNames = new string[] { "SettingsKey" };
            var entityName = "GlobalSettings";
            var keyExp = new Dictionary<string, string>
            {
            };
            var groups = (from t in sysSettings select t.ParamName.ToLower()).Union(from t in upSysSettings select t.ParamName).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysSettings on c equals a1.ParamName.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysSettings on c equals a2.ParamName.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.ParamName}" },
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.ParamName));
                    change.Details.Add(MakeDetail("SettingsValue", c.New.Value, multiline: true));
                    change.Details.Add(MakeDetail("JsonSetting", c.New.IsJsonSetting.ToString(), "Entity.JsonSetting=(NewValueRaw==\"True\")"));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.ParamName}" },
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.Value != c.Original.Value && !string.IsNullOrWhiteSpace(c.New.Value) && !string.IsNullOrWhiteSpace(c.Original.Value)) || (string.IsNullOrWhiteSpace(c.New.Value) != string.IsNullOrWhiteSpace(c.Original.Value)))
                    {
                        change.Details.Add(MakeDetail("SettingsValue", c.New.Value, currentValue:c.Original.Value, multiline: true));
                    }

                    if (c.New.IsJsonSetting!= c.Original.IsJsonSetting)
                    {
                        change.Details.Add(MakeDetail("JsonSetting", c.New.IsJsonSetting.ToString(), "Entity.JsonSetting=(NewValueRaw==\"True\")", c.Original.IsJsonSetting.ToString()));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessGlobalSettingChange(change, c.New, c.Original);
                }
            }
        }

        private void CompareAuthenticationTypeClaims(AuthenticationTypeClaimTemplateMarkup[] sysAuthenticationTypeClaimTemplates, AuthenticationTypeClaimTemplateMarkup[] upSysAuthenticationTypeClaimTemplates)
        {
            var keyNames = new string[]{"AuthenticationType", "IncomingClaimName", "OutgoingClaimName", "Condition"};
            var entityName = "AuthenticationClaimMappings";
            var keyExp = new Dictionary<string, string>
            {
                {keyNames[0], MakeLinqQuery<TContext>("AuthenticationTypes", "AuthenticationTypeName", filterValueVariable: "Value")}
            };
            var groups = (from t in sysAuthenticationTypeClaimTemplates select new {t.AuthenticationTypeName, t.IncomingClaimName, t.OutgoingClaimName, t.Condition}).Union(from t in upSysAuthenticationTypeClaimTemplates select new { t.AuthenticationTypeName, t.IncomingClaimName, t.OutgoingClaimName, t.Condition }).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysAuthenticationTypeClaimTemplates on c equals new{a1.AuthenticationTypeName, a1.IncomingClaimName, a1.OutgoingClaimName, a1.Condition} into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysAuthenticationTypeClaimTemplates on c equals new {a2.AuthenticationTypeName, a2.IncomingClaimName, a2.OutgoingClaimName, a2.Condition} into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change { ChangeType = ChangeType.Delete, Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.AuthenticationTypeName}" },
                        {keyNames[1], c.Original.IncomingClaimName},
                        {keyNames[2], c.Original.OutgoingClaimName},
                        {keyNames[3], c.Original.Condition}
                    }, EntityName = entityName, Apply = true, KeyExpression = keyExp };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.AuthenticationTypeName, MakeLinqAssign<TContext>(keyNames[0], "AuthenticationTypes", "AuthenticationTypeName")));
                    change.Details.Add(MakeDetail(keyNames[1], c.New.IncomingClaimName));
                    change.Details.Add(MakeDetail(keyNames[2], c.New.OutgoingClaimName));
                    change.Details.Add(MakeDetail(keyNames[3], c.New.Condition));
                    change.Details.Add(MakeDetail("OutgoingClaimValue", c.New.OutgoingClaimValue));
                    change.Details.Add(MakeDetail("OutgoingIssuer", c.New.OutgoingIssuer));
                    change.Details.Add(MakeDetail("OutgoingOriginalIssuer", c.New.OutgoingOriginalIssuer));
                    change.Details.Add(MakeDetail("OutgoingValueType", c.New.OutgoingValueType));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change { ChangeType = ChangeType.Update, 
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.AuthenticationTypeName}" },
                            {keyNames[1], c.Original.IncomingClaimName},
                            {keyNames[2], c.Original.OutgoingClaimName},
                            {keyNames[3], c.Original.Condition}
                        }, EntityName = entityName, Apply = true, KeyExpression = keyExp };
                    if ((c.New.OutgoingClaimValue != c.Original.OutgoingClaimValue && !string.IsNullOrWhiteSpace(c.New.OutgoingClaimValue) && !string.IsNullOrWhiteSpace(c.Original.OutgoingClaimValue)) || (string.IsNullOrWhiteSpace(c.New.OutgoingClaimValue) != string.IsNullOrWhiteSpace(c.Original.OutgoingClaimValue)))
                    {
                        change.Details.Add(MakeDetail("OutgoingClaimValue", c.New.OutgoingClaimValue, currentValue: c.Original.OutgoingClaimValue));
                    }

                    if ((c.New.OutgoingIssuer != c.Original.OutgoingIssuer && !string.IsNullOrWhiteSpace(c.New.OutgoingIssuer) && !string.IsNullOrWhiteSpace(c.Original.OutgoingIssuer)) || (string.IsNullOrWhiteSpace(c.New.OutgoingIssuer) != string.IsNullOrWhiteSpace(c.Original.OutgoingIssuer)))
                    {
                        change.Details.Add(MakeDetail("OutgoingIssuer", c.New.OutgoingIssuer, currentValue: c.Original.OutgoingIssuer));
                    }

                    if ((c.New.OutgoingOriginalIssuer != c.Original.OutgoingOriginalIssuer && !string.IsNullOrWhiteSpace(c.New.OutgoingOriginalIssuer) && !string.IsNullOrWhiteSpace(c.Original.OutgoingOriginalIssuer)) || (string.IsNullOrWhiteSpace(c.New.OutgoingOriginalIssuer) != string.IsNullOrWhiteSpace(c.Original.OutgoingOriginalIssuer)))
                    {
                        change.Details.Add(MakeDetail("OutgoingOriginalIssuer", c.New.OutgoingOriginalIssuer, currentValue: c.Original.OutgoingOriginalIssuer));
                    }

                    if ((c.New.OutgoingValueType != c.Original.OutgoingValueType && !string.IsNullOrWhiteSpace(c.New.OutgoingValueType) && !string.IsNullOrWhiteSpace(c.Original.OutgoingValueType)) || (string.IsNullOrWhiteSpace(c.New.OutgoingValueType) != string.IsNullOrWhiteSpace(c.Original.OutgoingValueType)))
                    {
                        change.Details.Add(MakeDetail("OutgoingValueType", c.New.OutgoingValueType, currentValue: c.Original.OutgoingValueType));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessAuthenticationTypeClaimChange(change, c.New, c.Original);
                }
            }
        }

        private void CompareAuthenticationTypes(AuthenticationTypeTemplateMarkup[] sysAuthenticationTypes, AuthenticationTypeTemplateMarkup[] upSysAuthenticationTypes)
        {
            var keyNames = new []{"AuthenticationTypeName"};
            var entityName = "AuthenticationTypes";
            var keyExp = new Dictionary<string, string>
            {
            };
            var groups = (from t in sysAuthenticationTypes select t.AuthenticationTypeName.ToLower()).Union(from t in upSysAuthenticationTypes select t.AuthenticationTypeName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysAuthenticationTypes on c equals a1.AuthenticationTypeName.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysAuthenticationTypes on c equals a2.AuthenticationTypeName.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = na1?.AuthenticationTypeName ?? na2.AuthenticationTypeName, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change { ChangeType = ChangeType.Delete, Key = new Dictionary<string, string> {{ keyNames[0],$"{c.Original.AuthenticationTypeName}" } }, EntityName = entityName, Apply = true, KeyExpression = keyExp };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.AuthenticationTypeName));
                    RegisterChange(change);
                }

                if (change != null)
                {
                    PostProcessAuthenticationTypeChange(change, c.New, c.Original);
                }
            }
        }

        private void CompareGlobalRoles(GlobalRoleTemplateMarkup[] sysRoles, GlobalRoleTemplateMarkup[] upSysRoles)
        {
            var keyName = "RoleName";
            var entityName = "GlobalRoles";
            var allRoles = (from t in sysRoles select t.RoleName.ToLower()).Union(from t in upSysRoles select t.RoleName.ToLower()).Distinct().ToArray();
            var cmp = (from c in allRoles
                       join a1 in sysRoles on c equals a1.RoleName.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysRoles on c equals a2.RoleName.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = na1?.RoleName ?? na2.RoleName, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change { ChangeType = ChangeType.Delete, Key = new Dictionary<string, string> { { keyName, $"{c.Original.RoleName}" }}, EntityName = entityName, Apply = true, KeyExpression = new Dictionary<string, string>{ } };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyName, c.New.RoleName));
                    change.Details.Add(MakeDetail("RoleDescription", c.New.RoleDescription, multiline: true));
                    RegisterChange(change);
                    RegisterGlobalRolePerms(c.New.RoleName, c.New.Permissions);
                }
                else if (c.Original != null)
                {
                    change = new Change { ChangeType = ChangeType.Update, Key = new Dictionary<string, string> { { keyName, $"{c.Original.RoleName}" }, { "TenantId", null } }, EntityName = entityName, Apply = true, KeyExpression = new Dictionary<string, string>{} };
                    if ((c.New.RoleDescription != c.Original.RoleDescription && !string.IsNullOrWhiteSpace(c.New.RoleDescription) && !string.IsNullOrWhiteSpace(c.Original.RoleDescription)) || (string.IsNullOrWhiteSpace(c.New.RoleDescription) != string.IsNullOrWhiteSpace(c.Original.RoleDescription)))
                    {
                        change.Details.Add(MakeDetail("RoleDescription", c.New.RoleDescription, currentValue: c.Original.RoleDescription, multiline: true));
                    }
                    
                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }

                    RegisterGlobalRolePerms(c.New.RoleName, c.New.Permissions, c.Original.Permissions);
                }

                if (change != null)
                {
                    PostProcessGlobalRoleChange(change, c.New, c.Original);
                }
            }
        }

        private void RegisterGlobalRolePerms(string roleName, string[] permissions, string[] originalPermissions = null)
        {
            originalPermissions ??= Array.Empty<string>();
            var keyNames = new string[] { "GlobalRole", "Permission" };
            var entityName = "GlobalRolePermissions";
            var keyExp = new Dictionary<string, string>
            {
                {keyNames[0], MakeLinqQuery<TContext>("GlobalRoles", "RoleName", filterValueVariable: "Value")},
                {keyNames[1], MakeLinqQuery<TContext>("Permissions","PermissionName", additionalWhere:"n.TenantId==null", filterValueVariable:"Value")}
            };
            var groups = (from t in originalPermissions select t.ToLower()).Union(from t in permissions select t.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in originalPermissions on c equals a1.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in permissions on c equals a2.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[1], $"{c.Original}" },
                        {keyNames[0], roleName}
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[1], c.New, MakeLinqAssign<TContext>(keyNames[1], "Permissions", "PermissionName")));
                    change.Details.Add(MakeDetail(keyNames[0], roleName, MakeLinqAssign<TContext>(keyNames[0], "GlobalRoles", "RoleName")));
                    RegisterChange(change);
                }

                if (change != null)
                {
                    PostProcessGlobalRolePermissionChange(roleName, change, c.New, c.Original);
                }
            }
        }

        /// <summary>
        /// Compares the permission-configurations between two systems
        /// </summary>
        /// <param name="sysPermissions">the current system that is the compare-target</param>
        /// <param name="upSysPermissions">the system-definition that was uploaded as json</param>
        private void ComparePermissions(PermissionTemplateMarkup[] sysPermissions, PermissionTemplateMarkup[] upSysPermissions)
        {
            var keyName = "PermissionName";
            var entityName = "Permissions";
            string keyExp = null;
            var groups = (from t in sysPermissions select t.Name.ToLower()).Union(from t in upSysPermissions select t.Name.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysPermissions on c equals a1.Name.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysPermissions on c equals a2.Name.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = na1?.Name ?? na2.Name, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change { ChangeType = ChangeType.Delete, Key = new Dictionary<string, string> { { keyName, $"{c.Original.Name}" }, {"TenantId", null} }, EntityName = entityName, Apply = true, KeyExpression = new Dictionary<string, string>{ } };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyName, c.New.Name));
                    change.Details.Add(MakeDetail("Description", c.New.Description, multiline: true));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change { ChangeType = ChangeType.Update, Key = new Dictionary<string, string> { { keyName, $"{c.Original.Name}" }, { "TenantId", null } }, EntityName = entityName, Apply = true, KeyExpression = new Dictionary<string, string>{} };
                    if ((c.New.Description != c.Original.Description && !string.IsNullOrWhiteSpace(c.New.Description) && !string.IsNullOrWhiteSpace(c.Original.Description)) || (string.IsNullOrWhiteSpace(c.New.Description) != string.IsNullOrWhiteSpace(c.Original.Description)))
                    {
                        change.Details.Add(MakeDetail("Description", c.New.Description, currentValue: c.Original.Description, multiline: true));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessPermissionChange(change, c.New, c.Original);
                }
            }
        }

        /// <summary>
        /// Compares the const-configurations between two systems
        /// </summary>
        /// <param name="sysConstants">the current system that is the compare-target</param>
        /// <param name="upSysConstants">the system-definition that was uploaded as json</param>
        private void CompareConstants(ConstTemplateMarkup[] sysConstants, ConstTemplateMarkup[] upSysConstants)
        {
            var keyName = "Name";
            var entityName = "WebPluginConstants";
            string keyExp = null;
            var groups = (from t in sysConstants select t.Name.ToLower()).Union(from t in upSysConstants select t.Name.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in sysConstants on c equals a1.Name.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in upSysConstants on c equals a2.Name.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Name = na1?.Name?? na2.Name, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change { ChangeType = ChangeType.Delete, Key = new Dictionary<string, string> { { keyName, $"{c.Original.Name}" }, {"TenantId", null} }, EntityName = entityName, Apply = true, KeyExpression = new Dictionary<string, string> {}};
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyName, c.New.Name));
                    change.Details.Add(MakeDetail("Value", c.New.Value));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change { ChangeType = ChangeType.Update, Key = new Dictionary<string, string> { { keyName, $"{c.Original.Name}" }, { "TenantId", null } }, EntityName = entityName, Apply = true, KeyExpression = new Dictionary<string, string>{} };
                    if ((c.New.Value != c.Original.Value && !string.IsNullOrWhiteSpace(c.New.Value) && !string.IsNullOrWhiteSpace(c.Original.Value)) || (string.IsNullOrWhiteSpace(c.New.Value) != string.IsNullOrWhiteSpace(c.Original.Value)))
                    {
                        change.Details.Add(MakeDetail("Value", c.New.Value, currentValue: c.Original.Value));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessConstantChange(change, c.New, c.Original);
                }
            }
        }

        /// <summary>
        /// Compares the plugin-configurations between two systems
        /// </summary>
        /// <param name="sysPlugins">the current system that is the compare-target</param>
        /// <param name="upSysPlugins">the system-definition that was uploaded as json</param>
        private void ComparePlugIns(IList<PlugInTemplateMarkup> sysPlugins, IList<PlugInTemplateMarkup> upSysPlugins)
        {
            var keyName = "UniqueName";
            var entityName = "WebPlugins";
            string keyExp = null;
            var groups = (from t in sysPlugins select t.UniqueName.ToLower()).Union(from t in upSysPlugins select t.UniqueName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in sysPlugins on c equals a1.UniqueName.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in upSysPlugins on c equals a2.UniqueName.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = na1?.UniqueName ?? na2.UniqueName, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change { ChangeType = ChangeType.Delete, Key = new Dictionary<string, string> {{keyName, $"{c.Original.UniqueName}" }, { "TenantId", null } }, EntityName = entityName, Apply = true };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName  = "WebPlugins", Apply = true };
                    change.Details.Add(MakeDetail(keyName, c.New.UniqueName));
                    change.Details.Add(MakeDetail("Constructor", c.New.Constructor));
                    change.Details.Add(MakeDetail("AutoLoad", c.New.AutoLoad.ToString(), "Entity.AutoLoad=(NewValueRaw==\"True\")"));
                    RegisterChange(change);
                    RegisterPluginParameters(c.New.UniqueName, c.New.GenericArguments);
                }
                else if (c.Original != null)
                {
                    change = new Change { ChangeType = ChangeType.Update, Key = new Dictionary<string, string> { { keyName, $"{c.Original.UniqueName}" }, { "TenantId", null } }, EntityName = "WebPlugins", Apply = true };
                    if ((c.New.Constructor != c.Original.Constructor && !string.IsNullOrWhiteSpace(c.New.Constructor) && !string.IsNullOrWhiteSpace(c.Original.Constructor)) || (string.IsNullOrWhiteSpace(c.New.Constructor) != string.IsNullOrWhiteSpace(c.Original.Constructor)))
                    {
                        change.Details.Add(MakeDetail("Constructor", c.New.Constructor, currentValue: c.Original.Constructor));
                    }

                    if (c.New.AutoLoad != c.Original.AutoLoad)
                    {
                        change.Details.Add(MakeDetail("AutoLoad", c.New.AutoLoad.ToString(), "Entity.AutoLoad=(NewValueRaw==\"True\")", c.Original.AutoLoad.ToString()));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }

                    RegisterPluginParameters(c.New.UniqueName, c.New.GenericArguments, c.Original.GenericArguments);
                }

                if (change != null)
                {
                    PostProcessPlugInChange(change, c.New, c.Original);
                }
            }
        }

        protected virtual void PostProcessPlugInChange(Change change, PlugInTemplateMarkup @new, PlugInTemplateMarkup original)
        {
        }
        protected virtual void PostProcessConstantChange(Change change, ConstTemplateMarkup @new, ConstTemplateMarkup original)
        {
        }
        protected virtual void PostProcessPermissionChange(Change change, PermissionTemplateMarkup @new, PermissionTemplateMarkup original)
        {
        }
        protected virtual void PostProcessGlobalRoleChange(Change change, GlobalRoleTemplateMarkup @new, GlobalRoleTemplateMarkup original)
        {
        }
        protected virtual void PostProcessAuthenticationTypeChange(Change change, AuthenticationTypeTemplateMarkup @new, AuthenticationTypeTemplateMarkup original)
        {
        }
        protected virtual void PostProcessAuthenticationTypeClaimChange(Change change, AuthenticationTypeClaimTemplateMarkup @new, AuthenticationTypeClaimTemplateMarkup original)
        {
        }
        protected virtual void PostProcessGlobalSettingChange(Change change, SettingTemplateMarkup @new, SettingTemplateMarkup original)
        {
        }
        protected virtual void PostProcessFeatureChange(Change change, SystemFeatureTemplateMarkup @new, SystemFeatureTemplateMarkup original)
        {
        }
        protected virtual void PostProcessTenantTemplateChange(Change change, TenantTemplateDefinitionMarkup @new, TenantTemplateDefinitionMarkup original)
        {
        }
        protected virtual void PostProcessDiagnosticsQueryChange(Change change, DiagnosticsQueryTemplateMarkup @new, DiagnosticsQueryTemplateMarkup original)
        {
        }
        protected virtual void PostProcessDashboardWidgetChange(Change change, DashboardWidgetTemplateMarkup @new, DashboardWidgetTemplateMarkup original)
        {
        }
        protected virtual void PostProcessDashboardWidgetLocaleChange(Change change, DashboardWidgetLocaleTemplateMarkup @new, DashboardWidgetLocaleTemplateMarkup original)
        {
        }
        protected virtual void PostProcessNavigationChange(Change change, NavigationMenuTemplateMarkup @new, NavigationMenuTemplateMarkup original)
        {
        }
        protected virtual void PostProcessTrustedModuleChange(Change change, TrustedModuleTemplateMarkup @new, TrustedModuleTemplateMarkup original)
        {
        }
        protected virtual void PostProcessHealthScriptChange(Change change, HealthScriptTemplateMarkup @new, HealthScriptTemplateMarkup original)
        {
        }

        protected virtual void PostProcessAssetTemplateChange(Change change, AssetTemplateMarkup @new, AssetTemplateMarkup original)
        {
        }

        protected virtual void PostProcessPluginParameterChange(string pluginUniqueName, Change change, PlugInGenericArgumentTemplateMarkup @new, PlugInGenericArgumentTemplateMarkup original)
        {
        }

        protected virtual void PostProcessGlobalRolePermissionChange(string roleName, Change change, string @new, string original)
        {

        }

        protected virtual void PostProcessQueryParameterChange(string diagnosticsQueryName, Change change, DiagnosticsQueryParameterTemplateMarkup @new, DiagnosticsQueryParameterTemplateMarkup original)
        {
        }

        protected virtual void PostProcessAssetPathFilterChange(string systemKey, Change change, string @new, string original)
        {
        }

        protected virtual void PostProcessAssetFeatureChange(string systemKey, Change change, string @new, string original)
        {
        }

        protected virtual void PostProcessAssetPermissionChange(string systemKey, Change change, string @new, string original)
        {
            throw new NotImplementedException();
        }

        protected virtual void PostProcessWidgetParameterChange(string dashboardName, Change change, DashboardParamTemplateMarkup @new, DashboardParamTemplateMarkup original)
        {
        }

        private void RegisterPluginParameters(string pluginUniqueName, PlugInGenericArgumentTemplateMarkup[] parameters, PlugInGenericArgumentTemplateMarkup[] originalParameters = null)
        {
            originalParameters ??= Array.Empty<PlugInGenericArgumentTemplateMarkup>();
            var keyNames = new string[] { "GenericTypeName", "Plugin" };
            var entityName = "GenericPluginParams";
            var keyExp = new Dictionary<string, string>
            {
                {keyNames[1], MakeLinqQuery<TContext>("WebPlugins", "UniqueName", filterValueVariable: "Value", additionalWhere:"n.TenantId == null")}
            };
            var groups = (from t in originalParameters select t.GenericTypeName.ToLower()).Union(from t in parameters select t.GenericTypeName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                       join a1 in originalParameters on c equals a1.GenericTypeName.ToLower() into ja1
                       from na1 in ja1.DefaultIfEmpty()
                       join a2 in parameters on c equals a2.GenericTypeName.ToLower() into ja2
                       from na2 in ja2.DefaultIfEmpty()
                       select new { Name = c, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string>
                    {
                        { keyNames[0], $"{c.Original.GenericTypeName}" },
                        {keyNames[1], pluginUniqueName}
                    },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New.GenericTypeName));
                    change.Details.Add(MakeDetail(keyNames[1], pluginUniqueName, MakeLinqAssign<TContext>(keyNames[1], "WebPlugins", "UniqueName")));
                    change.Details.Add(MakeDetail("TypeExpression", c.New.TypeExpression));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string>
                        {
                            { keyNames[0], $"{c.Original.GenericTypeName}" },
                            {keyNames[1], pluginUniqueName}
                        },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.TypeExpression != c.Original.TypeExpression && !string.IsNullOrWhiteSpace(c.New.TypeExpression) && !string.IsNullOrWhiteSpace(c.Original.TypeExpression)) || (string.IsNullOrWhiteSpace(c.New.TypeExpression) != string.IsNullOrWhiteSpace(c.Original.TypeExpression)))
                    {
                        change.Details.Add(MakeDetail("TypeExpression", c.New.TypeExpression, currentValue: c.Original.TypeExpression));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessPluginParameterChange(pluginUniqueName, change, c.New, c.Original);
                }
            }
        }
    }
}
