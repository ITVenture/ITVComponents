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
using ITVComponents.Logging;
using ITVComponents.Plugins.Initialization;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Feature = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Feature;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ConfigurationHandler
{
    public abstract class SysConfigurationHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : ConfigurationHandlerBase, IConfigChangeContext
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

        public SysConfigurationHandler(TContext db, IServiceProvider services = null)
        {
            DbContext = db;
            this.services = services;
        }

        private readonly IServiceProvider services;

        private TContext DbContext { get; }

        // Resolved manually (the plugin system doesn't inject arbitrary DI deps into the ctor; only the
        // IServiceProvider is passed in). Empty options when no extension is registered.
        private ConfigExtensionOptions ExtensionOptions
            => services?.GetService<IOptions<ConfigExtensionOptions>>()?.Value ?? new ConfigExtensionOptions();

        // Same resolution as the extensions above. Without any configuration this still yields the built-in
        // Full/BasicOnly profiles, so the export behaves exactly as it did before profiles existed.
        private ConfigExportProfileOptions ProfileOptions
            => services?.GetService<IOptions<ConfigExportProfileOptions>>()?.Value ?? new ConfigExportProfileOptions();

        ChangeDetail IConfigChangeContext.MakeDetail(string columnName, string value, string valueExpression, string currentValue, bool multiline, bool apply)
            => MakeDetail(columnName, value, valueExpression, currentValue, multiline, apply);

        string IConfigChangeContext.MakeLinqAssign(string targetProperty, string sourceEntity, string filterProperty, string additionalWhere, bool ignoreFail, string managedFilterType, string scriptedFilterType)
            => MakeLinqAssign<TContext>(targetProperty, sourceEntity, filterProperty, additionalWhere, ignoreFail, managedFilterType, scriptedFilterType);

        string IConfigChangeContext.MakeLinqQuery(string sourceEntity, string filterProperty, string additionalWhere, bool ignoreFail, string managedFilterType, string scriptedFilterType, string filterValueVariable)
            => MakeLinqQuery<TContext>(sourceEntity, filterProperty, additionalWhere, ignoreFail, managedFilterType, scriptedFilterType, filterValueVariable);

        /// <summary>
        /// Builds the current (IST) markup for the config-extensions the given profile includes (null if none).
        /// A null profile means every registered extension.
        /// </summary>
        private List<ConfigExtensionMarkup> DescribeExtensions(ConfigExportProfile profile = null)
        {
            var opts = ExtensionOptions;
            if (services == null || opts.Handlers.Count == 0)
            {
                return null;
            }

            var result = new List<ConfigExtensionMarkup>();
            using var scope = services.CreateScope();
            foreach (var reg in opts.Handlers)
            {
                if (profile != null && !profile.IncludesExtension(reg.SectionKey))
                {
                    continue;
                }

                var handler = (IConfigExtension)ActivatorUtilities.CreateInstance(scope.ServiceProvider, reg.HandlerType);
                var markup = handler.Describe(DbContext);
                if (markup != null)
                {
                    markup.SectionKey = reg.SectionKey;
                    result.Add(markup);
                }
            }

            return result.Count > 0 ? result : null;
        }

        /// <summary>Dispatches each uploaded extension-section to its handler and registers the resulting changes.</summary>
        private void CompareExtensions(List<ConfigExtensionMarkup> current, List<ConfigExtensionMarkup> uploaded)
        {
            var opts = ExtensionOptions;
            if (services == null || uploaded == null || opts.Handlers.Count == 0)
            {
                return;
            }

            using var scope = services.CreateScope();
            foreach (var up in uploaded)
            {
                var reg = opts.Handlers.FirstOrDefault(h => string.Equals(h.SectionKey, up.SectionKey, StringComparison.OrdinalIgnoreCase));
                if (reg == null)
                {
                    continue; // section whose contributing library isn't installed here -> skip
                }

                var handler = (IConfigExtension)ActivatorUtilities.CreateInstance(scope.ServiceProvider, reg.HandlerType);
                var cur = current?.FirstOrDefault(c => string.Equals(c.SectionKey, up.SectionKey, StringComparison.OrdinalIgnoreCase));
                foreach (var change in handler.Compare(DbContext, cur, up, this))
                {
                    RegisterChange(change);
                }
            }
        }

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
            // A profile suffix is irrelevant here — what gets compared is decided by the content of the file, not
            // by what the uploader picked. It is only split off so a hint that carried one over (sysCfg@Help) is
            // not rejected as an unknown file-type.
            var baseType = ConfigExportProfiles.Split(fileType, out _);
            switch (baseType)
            {
                case "sysCfg":
                    using (var h = new FullSecurityAccessHelper<TTrustConfig>(DbContext, new() { ShowAllTenants = true, HideGlobals = false }))
                    {
                        var upSys =
                            JsonHelper.FromJsonString<SystemTemplateMarkup>(
                                Encoding.UTF8.GetString(content), SerializationTypingMode.NativePolymorphism);

                        // Describe only as much of the current state as the uploaded file actually claims: a
                        // Billing-only file must not make us read (and base64-encode) every help resource.
                        var comparedSections = new ConfigExportProfile
                        {
                            OmitBasicData = upSys.OmitBasicData,
                            ActiveExtensions = upSys.Extensions?.Select(n => n.SectionKey).ToArray() ?? Array.Empty<string>()
                        };
                        var sys = DescribeSystem(comparedSections);

                        if (!upSys.OmitBasicData)
                        {
                            CompareBasicData(sys, upSys);
                        }
                        else if (HasAnyBasicData(upSys))
                        {
                            // The flag wins, but not silently: someone hand-edited base data into a file that
                            // declares it carries none, and would otherwise wonder why nothing arrived.
                            RegisterChange(new Change
                            {
                                ChangeType = ChangeType.Warning,
                                EntityName = "Base configuration data was ignored",
                                Details =
                                {
                                    MakeDetail("Reason",
                                        "The uploaded file declares OmitBasicData, so its base sections are not compared. Export without that profile setting to transfer them.",
                                        apply: false)
                                }
                            });
                            LogEnvironment.LogEvent(
                                "An uploaded system-configuration declares OmitBasicData but still carries base sections; they were ignored.",
                                LogSeverity.Warning);
                        }

                        // Feature-library-contributed sections (e.g. Billing). Null on older exports / no extension.
                        CompareExtensions(sys.Extensions, upSys.Extensions);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"{fileType} not supported");
            }
        }

        /// <summary>
        /// Compares the base (non-extension) sections. Every section is guarded: one that the uploaded file does
        /// not carry is skipped rather than compared against an empty set — otherwise a partial export would come
        /// out of the diff as "delete the entire system configuration", pre-selected for apply.
        /// </summary>
        private void CompareBasicData(SystemTemplateMarkup sys, SystemTemplateMarkup upSys)
        {
            if (upSys.PlugIns != null)
            {
                ComparePlugIns(sys.PlugIns, upSys.PlugIns);
            }

            if (upSys.Constants != null)
            {
                CompareConstants(sys.Constants, upSys.Constants);
            }

            if (upSys.Permissions != null)
            {
                ComparePermissions(sys.Permissions, upSys.Permissions);
            }

            if (upSys.GlobalRoles != null)
            {
                CompareGlobalRoles(sys.GlobalRoles, upSys.GlobalRoles);
            }

            if (upSys.AuthenticationTypes != null)
            {
                CompareAuthenticationTypes(sys.AuthenticationTypes, upSys.AuthenticationTypes);
            }

            if (upSys.AuthenticationTypeClaimTemplates != null)
            {
                CompareAuthenticationTypeClaims(sys.AuthenticationTypeClaimTemplates, upSys.AuthenticationTypeClaimTemplates);
            }

            if (upSys.Settings != null)
            {
                CompareGlobalSettings(sys.Settings, upSys.Settings);
            }

            if (upSys.Features != null)
            {
                CompareFeatures(sys.Features, upSys.Features);
            }

            if (upSys.TenantTemplates != null)
            {
                CompareTenantTemplates(sys.TenantTemplates, upSys.TenantTemplates);
            }

            if (upSys.DiagnosticsQueries != null)
            {
                CompareDiagnosticsQueries(sys.DiagnosticsQueries, upSys.DiagnosticsQueries);
            }

            if (upSys.DashboardWidgets != null)
            {
                CompareDashboardWidgets(sys.DashboardWidgets, upSys.DashboardWidgets);
            }

            if (upSys.DashboardWidgetLocales != null)
            {
                CompareDashboardWidgetLocales(sys.DashboardWidgetLocales, upSys.DashboardWidgetLocales);
            }

            if (upSys.Navigation != null)
            {
                CompareNavigation(sys.Navigation, upSys.Navigation);
            }

            if (upSys.TrustedModules != null)
            {
                CompareTrustedModules(sys.TrustedModules, upSys.TrustedModules);
            }

            if (upSys.HealthScripts != null)
            {
                CompareHealthScripts(sys.HealthScripts, upSys.HealthScripts);
            }

            if (upSys.AssetTemplates != null)
            {
                CompareAssetTemplates(sys.AssetTemplates, upSys.AssetTemplates);
            }

            if (upSys.ExternalOAuthServices != null)
            {
                CompareExternalOAuthServices(sys.ExternalOAuthServices, upSys.ExternalOAuthServices);
            }

            if (upSys.TemplateModules != null)
            {
                CompareTemplateModules(sys.TemplateModules, upSys.TemplateModules);
            }
        }

        /// <summary>True when the markup carries at least one base section (used to detect a hand-edited file).</summary>
        private static bool HasAnyBasicData(SystemTemplateMarkup markup)
            => markup.PlugIns != null || markup.Constants != null || markup.Permissions != null
               || markup.GlobalRoles != null || markup.AuthenticationTypes != null
               || markup.AuthenticationTypeClaimTemplates != null || markup.Settings != null
               || markup.Features != null || markup.TenantTemplates != null || markup.DiagnosticsQueries != null
               || markup.DashboardWidgets != null || markup.DashboardWidgetLocales != null
               || markup.Navigation != null || markup.TrustedModules != null || markup.HealthScripts != null
               || markup.AssetTemplates != null || markup.ExternalOAuthServices != null
               || markup.TemplateModules != null;

        public override object DescribeConfig(string fileType, IDictionary<string, int> filterDic, out string name)
        {
            var baseType = ConfigExportProfiles.Split(fileType, out var profileName);
            switch (baseType)
            {
                case "sysCfg":
                    var profiles = ProfileOptions.EffectiveProfiles();
                    if (!string.IsNullOrWhiteSpace(profileName) && !profiles.ContainsKey(profileName))
                    {
                        // A misspelled profile would otherwise silently produce a full export under a name that
                        // promises something else.
                        LogEnvironment.LogEvent(
                            $"Unknown config-export profile '{profileName}' was requested; falling back to '{ConfigExportProfiles.Full}'.",
                            LogSeverity.Warning);
                        profileName = null;
                    }

                    var profile = ProfileOptions.Resolve(profileName);
                    var sys = DescribeSystem(profile, profileName);
                    // The profile belongs in the download name — otherwise three different exports all land in
                    // the download folder as System.json.
                    name = string.IsNullOrWhiteSpace(profileName) ? "System" : $"System_{profileName}";
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

        /// <summary>
        /// Builds the current (IST) markup. A profile restricts what is produced: with
        /// <see cref="ConfigExportProfile.OmitBasicData"/> the base sections are not even read (they stay null,
        /// and the file says so via <see cref="SystemTemplateMarkup.OmitBasicData"/>), and only the profile's
        /// extension sections are described — which also keeps a Billing-only export from loading every help
        /// resource just to throw it away.
        /// </summary>
        private SystemTemplateMarkup DescribeSystem(ConfigExportProfile profile = null, string profileName = null)
        {
            using (var h = new FullSecurityAccessHelper<TTrustConfig>(DbContext, new() { ShowAllTenants = true, HideGlobals = false }))
            {
                // Omitted base data is not read at all — the empty markup plus the OmitBasicData flag is what
                // tells the receiving system that those sections are simply not claimed by this file.
                var retVal = profile is { OmitBasicData: true } ? new SystemTemplateMarkup() : DescribeBasicData();
                retVal.ExportProfile = profileName ?? ConfigExportProfiles.Full;
                retVal.OmitBasicData = profile?.OmitBasicData ?? false;
                retVal.Extensions = DescribeExtensions(profile);

                return retVal;
            }
        }

        /// <summary>Reads every base (non-extension) section of the system configuration.</summary>
        private SystemTemplateMarkup DescribeBasicData()
        {
            DbContext.EnsureNavUniqueness();
            return new SystemTemplateMarkup
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
                    .Include(n => n.RequiredPermission).AsEnumerable().Select(n => SelectAssetTemplateMarkup(n)).ToArray(),
                ExternalOAuthServices = DbContext.ExternalOAuthServices.Where(n => n.TenantId == null).AsEnumerable()
                    .Select(n => SelectExternalOAuthServiceTemplateMarkup(n)).ToArray(),
                TemplateModules = DbContext.TemplateModules.Include(n => n.RequiredFeature)
                    .Include(n => n.Configurators).ThenInclude(c => c.ViewComponentParameters)
                    .Include(n => n.Scripts).AsEnumerable().Select(n => SelectTemplateModuleTemplateMarkup(n)).ToArray()
            };
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

        protected virtual ExternalOAuthServiceTemplateMarkup SelectExternalOAuthServiceTemplateMarkup(TExternalOAuthService serviceInst)
        {
            return new ExternalOAuthServiceTemplateMarkup
            {
                UniqueConnectionName = serviceInst.UniqueConnectionName,
                AuthorizationEndpoint = serviceInst.AuthorizationEndpoint,
                TokenEndpoint = serviceInst.TokenEndpoint,
                RevocationEndpoint = serviceInst.RevocationEndpoint,
                ClientId = serviceInst.ClientId,
                Scope = serviceInst.Scope,
                Global = serviceInst.Global,
                AuthenticationType = serviceInst.AuthenticationType
                // NOTE: ClientSecret is deliberately NOT extracted — secrets must not travel with the system config.
            };
        }

        protected virtual TemplateModuleTemplateMarkup SelectTemplateModuleTemplateMarkup(TemplateModule moduleInst)
        {
            return new TemplateModuleTemplateMarkup
            {
                TemplateModuleName = moduleInst.TemplateModuleName,
                RequiredFeature = moduleInst.RequiredFeature?.FeatureName,
                Configurators = moduleInst.Configurators.Select(c => SelectTemplateModuleConfiguratorTemplateMarkup(c)).ToArray(),
                Scripts = moduleInst.Scripts.Select(s => s.ScriptFile).ToArray()
            };
        }

        protected virtual TemplateModuleConfiguratorTemplateMarkup SelectTemplateModuleConfiguratorTemplateMarkup(TemplateModuleConfigurator configuratorInst)
        {
            return new TemplateModuleConfiguratorTemplateMarkup
            {
                Name = configuratorInst.Name,
                DisplayName = configuratorInst.DisplayName,
                CustomConfiguratorView = configuratorInst.CustomConfiguratorView,
                ConfiguratorTypeBack = configuratorInst.ConfiguratorTypeBack,
                Parameters = configuratorInst.ViewComponentParameters.Select(p => SelectTemplateModuleConfiguratorParameterTemplateMarkup(p)).ToArray()
            };
        }

        protected virtual TemplateModuleConfiguratorParameterTemplateMarkup SelectTemplateModuleConfiguratorParameterTemplateMarkup(TemplateModuleConfiguratorParameter parameterInst)
        {
            return new TemplateModuleConfiguratorParameterTemplateMarkup
            {
                ParameterName = parameterInst.ParameterName,
                DisplayName = parameterInst.DisplayName,
                ParameterValue = parameterInst.ParameterValue
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
                RendererKey = dashboardWidgetInst.RendererKey, RendererOptions = dashboardWidgetInst.RendererOptions,
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
                    change.Details.Add(MakeDetail("RendererKey", c.New.RendererKey));
                    change.Details.Add(MakeDetail("RendererOptions", c.New.RendererOptions, multiline: true));
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

                    // Der Renderer und seine Einstellungen gehoeren zusammen mit dem Template abgeglichen:
                    // ein Diagramm-Widget, das ohne sie zurueckkaeme, wuerde seine Deklaration als Scriban
                    // lesen und den Rohtext in die Kachel schreiben.
                    if ((c.New.RendererKey != c.Original.RendererKey && !string.IsNullOrWhiteSpace(c.New.RendererKey) && !string.IsNullOrWhiteSpace(c.Original.RendererKey)) || (string.IsNullOrWhiteSpace(c.New.RendererKey) != string.IsNullOrWhiteSpace(c.Original.RendererKey)))
                    {
                        change.Details.Add(MakeDetail("RendererKey", c.New.RendererKey, currentValue: c.Original.RendererKey));
                    }

                    if ((c.New.RendererOptions != c.Original.RendererOptions && !string.IsNullOrWhiteSpace(c.New.RendererOptions) && !string.IsNullOrWhiteSpace(c.Original.RendererOptions)) || (string.IsNullOrWhiteSpace(c.New.RendererOptions) != string.IsNullOrWhiteSpace(c.Original.RendererOptions)))
                    {
                        change.Details.Add(MakeDetail("RendererOptions", c.New.RendererOptions, currentValue: c.Original.RendererOptions, multiline: true));
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
        /// Compares the global (TenantId == null) external-oauth-service configurations between two systems.
        /// ClientSecret is never transported; on insert the secret is created empty and must be set on the target afterwards.
        /// </summary>
        /// <param name="sysServices">the current system that is the compare-target</param>
        /// <param name="upSysServices">the system-definition that was uploaded as json</param>
        private void CompareExternalOAuthServices(ExternalOAuthServiceTemplateMarkup[] sysServices, ExternalOAuthServiceTemplateMarkup[] upSysServices)
        {
            sysServices ??= Array.Empty<ExternalOAuthServiceTemplateMarkup>();
            var keyName = "UniqueConnectionName";
            var entityName = "ExternalOAuthServices";
            var groups = (from t in sysServices select t.UniqueConnectionName.ToLower()).Union(from t in upSysServices select t.UniqueConnectionName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in sysServices on c equals a1.UniqueConnectionName.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in upSysServices on c equals a2.UniqueConnectionName.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Name = na1?.UniqueConnectionName ?? na2.UniqueConnectionName, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change { ChangeType = ChangeType.Delete, Key = new Dictionary<string, string> { { keyName, $"{c.Original.UniqueConnectionName}" }, { "TenantId", null } }, EntityName = entityName, Apply = true };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyName, c.New.UniqueConnectionName));
                    change.Details.Add(MakeDetail("AuthorizationEndpoint", c.New.AuthorizationEndpoint));
                    change.Details.Add(MakeDetail("TokenEndpoint", c.New.TokenEndpoint));
                    change.Details.Add(MakeDetail("RevocationEndpoint", c.New.RevocationEndpoint));
                    change.Details.Add(MakeDetail("ClientId", c.New.ClientId));
                    change.Details.Add(MakeDetail("Scope", c.New.Scope));
                    change.Details.Add(MakeDetail("Global", c.New.Global.ToString(), "Entity.Global=(NewValueRaw==\"True\")"));
                    change.Details.Add(MakeDetail("AuthenticationType", c.New.AuthenticationType.ToString()));
                    // ClientSecret is required by the schema but must not be cloned -> create empty; target-admin sets it later.
                    change.Details.Add(MakeDetail("ClientSecret", string.Empty));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change { ChangeType = ChangeType.Update, Key = new Dictionary<string, string> { { keyName, $"{c.Original.UniqueConnectionName}" }, { "TenantId", null } }, EntityName = entityName, Apply = true };
                    if ((c.New.AuthorizationEndpoint != c.Original.AuthorizationEndpoint && !string.IsNullOrWhiteSpace(c.New.AuthorizationEndpoint) && !string.IsNullOrWhiteSpace(c.Original.AuthorizationEndpoint)) || (string.IsNullOrWhiteSpace(c.New.AuthorizationEndpoint) != string.IsNullOrWhiteSpace(c.Original.AuthorizationEndpoint)))
                    {
                        change.Details.Add(MakeDetail("AuthorizationEndpoint", c.New.AuthorizationEndpoint, currentValue: c.Original.AuthorizationEndpoint));
                    }

                    if ((c.New.TokenEndpoint != c.Original.TokenEndpoint && !string.IsNullOrWhiteSpace(c.New.TokenEndpoint) && !string.IsNullOrWhiteSpace(c.Original.TokenEndpoint)) || (string.IsNullOrWhiteSpace(c.New.TokenEndpoint) != string.IsNullOrWhiteSpace(c.Original.TokenEndpoint)))
                    {
                        change.Details.Add(MakeDetail("TokenEndpoint", c.New.TokenEndpoint, currentValue: c.Original.TokenEndpoint));
                    }

                    if ((c.New.RevocationEndpoint != c.Original.RevocationEndpoint && !string.IsNullOrWhiteSpace(c.New.RevocationEndpoint) && !string.IsNullOrWhiteSpace(c.Original.RevocationEndpoint)) || (string.IsNullOrWhiteSpace(c.New.RevocationEndpoint) != string.IsNullOrWhiteSpace(c.Original.RevocationEndpoint)))
                    {
                        change.Details.Add(MakeDetail("RevocationEndpoint", c.New.RevocationEndpoint, currentValue: c.Original.RevocationEndpoint));
                    }

                    if ((c.New.ClientId != c.Original.ClientId && !string.IsNullOrWhiteSpace(c.New.ClientId) && !string.IsNullOrWhiteSpace(c.Original.ClientId)) || (string.IsNullOrWhiteSpace(c.New.ClientId) != string.IsNullOrWhiteSpace(c.Original.ClientId)))
                    {
                        change.Details.Add(MakeDetail("ClientId", c.New.ClientId, currentValue: c.Original.ClientId));
                    }

                    if ((c.New.Scope != c.Original.Scope && !string.IsNullOrWhiteSpace(c.New.Scope) && !string.IsNullOrWhiteSpace(c.Original.Scope)) || (string.IsNullOrWhiteSpace(c.New.Scope) != string.IsNullOrWhiteSpace(c.Original.Scope)))
                    {
                        change.Details.Add(MakeDetail("Scope", c.New.Scope, currentValue: c.Original.Scope));
                    }

                    if (c.New.Global != c.Original.Global)
                    {
                        change.Details.Add(MakeDetail("Global", c.New.Global.ToString(), "Entity.Global=(NewValueRaw==\"True\")", c.Original.Global.ToString()));
                    }

                    if (c.New.AuthenticationType != c.Original.AuthenticationType)
                    {
                        change.Details.Add(MakeDetail("AuthenticationType", c.New.AuthenticationType.ToString(), currentValue: c.Original.AuthenticationType.ToString()));
                    }

                    // ClientSecret is intentionally never updated here.
                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessExternalOAuthServiceChange(change, c.New, c.Original);
                }
            }
        }

        /// <summary>
        /// Compares the template-module configurations between two systems. Template-modules are a 3-level structure:
        /// module -> configurator -> parameter (plus module -> script). Configurators are unique only within their module,
        /// so the configurator/parameter level resolves its parent via a module-qualified, null-safe linq-filter.
        /// </summary>
        /// <param name="sysModules">the current system that is the compare-target</param>
        /// <param name="upSysModules">the system-definition that was uploaded as json</param>
        private void CompareTemplateModules(TemplateModuleTemplateMarkup[] sysModules, TemplateModuleTemplateMarkup[] upSysModules)
        {
            sysModules ??= Array.Empty<TemplateModuleTemplateMarkup>();
            var keyName = "TemplateModuleName";
            var entityName = "TemplateModules";
            var groups = (from t in sysModules select t.TemplateModuleName.ToLower()).Union(from t in upSysModules select t.TemplateModuleName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in sysModules on c equals a1.TemplateModuleName.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in upSysModules on c equals a2.TemplateModuleName.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Name = na1?.TemplateModuleName ?? na2.TemplateModuleName, Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    // Deleting the module cascades to its configurators/parameters/scripts via the required FKs.
                    change = new Change { ChangeType = ChangeType.Delete, Key = new Dictionary<string, string> { { keyName, $"{c.Original.TemplateModuleName}" } }, EntityName = entityName, Apply = true };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyName, c.New.TemplateModuleName));
                    change.Details.Add(MakeDetail("RequiredFeature", c.New.RequiredFeature, MakeLinqAssign<TContext>("RequiredFeature", "Features", "FeatureName")));
                    RegisterChange(change);
                    RegisterModuleConfigurators(c.New.TemplateModuleName, c.New.Configurators);
                    RegisterModuleScripts(c.New.TemplateModuleName, c.New.Scripts);
                }
                else if (c.Original != null)
                {
                    change = new Change { ChangeType = ChangeType.Update, Key = new Dictionary<string, string> { { keyName, $"{c.Original.TemplateModuleName}" } }, EntityName = entityName, Apply = true };
                    if ((c.New.RequiredFeature != c.Original.RequiredFeature && !string.IsNullOrWhiteSpace(c.New.RequiredFeature) && !string.IsNullOrWhiteSpace(c.Original.RequiredFeature)) || (string.IsNullOrWhiteSpace(c.New.RequiredFeature) != string.IsNullOrWhiteSpace(c.Original.RequiredFeature)))
                    {
                        change.Details.Add(MakeDetail("RequiredFeature", c.New.RequiredFeature, MakeLinqAssign<TContext>("RequiredFeature", "Features", "FeatureName"), c.Original.RequiredFeature));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }

                    RegisterModuleConfigurators(c.New.TemplateModuleName, c.New.Configurators, c.Original.Configurators);
                    RegisterModuleScripts(c.New.TemplateModuleName, c.New.Scripts, c.Original.Scripts);
                }

                if (change != null)
                {
                    PostProcessTemplateModuleChange(change, c.New, c.Original);
                }
            }
        }

        private void RegisterModuleScripts(string templateModuleName, string[] scripts, string[] originalScripts = null)
        {
            scripts ??= Array.Empty<string>();
            originalScripts ??= Array.Empty<string>();
            var keyNames = new[] { "ScriptFile", "ParentModule" };
            var entityName = "TemplateModuleScripts";
            var keyExp = new Dictionary<string, string>
            {
                { keyNames[1], MakeLinqQuery<TContext>("TemplateModules", "TemplateModuleName", filterValueVariable: "Value") }
            };
            var groups = (from t in originalScripts select t.ToLower()).Union(from t in scripts select t.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in originalScripts on c equals a1.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in scripts on c equals a2.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                // A script has no payload beyond its name + parent, so it can only be added or removed.
                if (c.Original != null && c.New == null)
                {
                    var change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string> { { keyNames[0], c.Original }, { keyNames[1], templateModuleName } },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    RegisterChange(change);
                }
                else if (c.Original == null && c.New != null)
                {
                    var change = new Change { ChangeType = ChangeType.Insert, EntityName = entityName, Apply = true };
                    change.Details.Add(MakeDetail(keyNames[0], c.New));
                    change.Details.Add(MakeDetail(keyNames[1], templateModuleName, MakeLinqAssign<TContext>(keyNames[1], "TemplateModules", "TemplateModuleName")));
                    RegisterChange(change);
                }
            }
        }

        private void RegisterModuleConfigurators(string templateModuleName, TemplateModuleConfiguratorTemplateMarkup[] configurators, TemplateModuleConfiguratorTemplateMarkup[] originalConfigurators = null)
        {
            configurators ??= Array.Empty<TemplateModuleConfiguratorTemplateMarkup>();
            originalConfigurators ??= Array.Empty<TemplateModuleConfiguratorTemplateMarkup>();
            var keyNames = new[] { "Name", "ParentModule" };
            var entityName = "TemplateModuleConfigurators";
            var keyExp = new Dictionary<string, string>
            {
                { keyNames[1], MakeLinqQuery<TContext>("TemplateModules", "TemplateModuleName", filterValueVariable: "Value") }
            };
            var groups = (from t in originalConfigurators select t.Name.ToLower()).Union(from t in configurators select t.Name.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in originalConfigurators on c equals a1.Name.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in configurators on c equals a2.Name.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    // Deleting the configurator cascades to its parameters via the required FK.
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string> { { keyNames[0], c.Original.Name }, { keyNames[1], templateModuleName } },
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
                    change.Details.Add(MakeDetail("ConfiguratorTypeBack", c.New.ConfiguratorTypeBack));
                    change.Details.Add(MakeDetail("CustomConfiguratorView", c.New.CustomConfiguratorView));
                    change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName));
                    change.Details.Add(MakeDetail(keyNames[1], templateModuleName, MakeLinqAssign<TContext>(keyNames[1], "TemplateModules", "TemplateModuleName")));
                    RegisterChange(change);
                    RegisterConfiguratorParameters(templateModuleName, c.New.Name, c.New.Parameters);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string> { { keyNames[0], c.Original.Name }, { keyNames[1], templateModuleName } },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.ConfiguratorTypeBack != c.Original.ConfiguratorTypeBack && !string.IsNullOrWhiteSpace(c.New.ConfiguratorTypeBack) && !string.IsNullOrWhiteSpace(c.Original.ConfiguratorTypeBack)) || (string.IsNullOrWhiteSpace(c.New.ConfiguratorTypeBack) != string.IsNullOrWhiteSpace(c.Original.ConfiguratorTypeBack)))
                    {
                        change.Details.Add(MakeDetail("ConfiguratorTypeBack", c.New.ConfiguratorTypeBack, currentValue: c.Original.ConfiguratorTypeBack));
                    }

                    if ((c.New.CustomConfiguratorView != c.Original.CustomConfiguratorView && !string.IsNullOrWhiteSpace(c.New.CustomConfiguratorView) && !string.IsNullOrWhiteSpace(c.Original.CustomConfiguratorView)) || (string.IsNullOrWhiteSpace(c.New.CustomConfiguratorView) != string.IsNullOrWhiteSpace(c.Original.CustomConfiguratorView)))
                    {
                        change.Details.Add(MakeDetail("CustomConfiguratorView", c.New.CustomConfiguratorView, currentValue: c.Original.CustomConfiguratorView));
                    }

                    if ((c.New.DisplayName != c.Original.DisplayName && !string.IsNullOrWhiteSpace(c.New.DisplayName) && !string.IsNullOrWhiteSpace(c.Original.DisplayName)) || (string.IsNullOrWhiteSpace(c.New.DisplayName) != string.IsNullOrWhiteSpace(c.Original.DisplayName)))
                    {
                        change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName, currentValue: c.Original.DisplayName));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }

                    RegisterConfiguratorParameters(templateModuleName, c.New.Name, c.New.Parameters, c.Original.Parameters);
                }

                if (change != null)
                {
                    PostProcessTemplateModuleConfiguratorChange(templateModuleName, change, c.New, c.Original);
                }
            }
        }

        private void RegisterConfiguratorParameters(string templateModuleName, string configuratorName, TemplateModuleConfiguratorParameterTemplateMarkup[] parameters, TemplateModuleConfiguratorParameterTemplateMarkup[] originalParameters = null)
        {
            parameters ??= Array.Empty<TemplateModuleConfiguratorParameterTemplateMarkup>();
            originalParameters ??= Array.Empty<TemplateModuleConfiguratorParameterTemplateMarkup>();
            var keyNames = new[] { "ParameterName", "ParentConfigurator" };
            var entityName = "TemplateModuleConfiguratorParameters";
            // Configurators are unique only per module, so resolve the parent configurator module-qualified.
            // The navigation check is null-guarded so the in-memory (Local) scan never NRE's on not-yet-loaded navigations;
            // existing configurators are matched through the translated SQL branch.
            var parentFilter = $"(n.ParentModule != null && n.ParentModule.TemplateModuleName == \"{templateModuleName}\")";
            var keyExp = new Dictionary<string, string>
            {
                { keyNames[1], MakeLinqQuery<TContext>("TemplateModuleConfigurators", "Name", additionalWhere: parentFilter, filterValueVariable: "Value") }
            };
            var groups = (from t in originalParameters select t.ParameterName.ToLower()).Union(from t in parameters select t.ParameterName.ToLower()).Distinct().ToArray();
            var cmp = (from c in groups
                join a1 in originalParameters on c equals a1.ParameterName.ToLower() into ja1
                from na1 in ja1.DefaultIfEmpty()
                join a2 in parameters on c equals a2.ParameterName.ToLower() into ja2
                from na2 in ja2.DefaultIfEmpty()
                select new { Original = na1, New = na2 }).ToArray();
            foreach (var c in cmp)
            {
                Change change = null;
                if (c.Original != null && c.New == null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Delete,
                        Key = new Dictionary<string, string> { { keyNames[0], c.Original.ParameterName }, { keyNames[1], configuratorName } },
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
                    change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName));
                    change.Details.Add(MakeDetail("ParameterValue", c.New.ParameterValue));
                    change.Details.Add(MakeDetail(keyNames[1], configuratorName, MakeLinqAssign<TContext>(keyNames[1], "TemplateModuleConfigurators", "Name", additionalWhere: parentFilter)));
                    RegisterChange(change);
                }
                else if (c.Original != null)
                {
                    change = new Change
                    {
                        ChangeType = ChangeType.Update,
                        Key = new Dictionary<string, string> { { keyNames[0], c.Original.ParameterName }, { keyNames[1], configuratorName } },
                        EntityName = entityName,
                        Apply = true,
                        KeyExpression = keyExp
                    };
                    if ((c.New.ParameterValue != c.Original.ParameterValue && !string.IsNullOrWhiteSpace(c.New.ParameterValue) && !string.IsNullOrWhiteSpace(c.Original.ParameterValue)) || (string.IsNullOrWhiteSpace(c.New.ParameterValue) != string.IsNullOrWhiteSpace(c.Original.ParameterValue)))
                    {
                        change.Details.Add(MakeDetail("ParameterValue", c.New.ParameterValue, currentValue: c.Original.ParameterValue));
                    }

                    if ((c.New.DisplayName != c.Original.DisplayName && !string.IsNullOrWhiteSpace(c.New.DisplayName) && !string.IsNullOrWhiteSpace(c.Original.DisplayName)) || (string.IsNullOrWhiteSpace(c.New.DisplayName) != string.IsNullOrWhiteSpace(c.Original.DisplayName)))
                    {
                        change.Details.Add(MakeDetail("DisplayName", c.New.DisplayName, currentValue: c.Original.DisplayName));
                    }

                    if (change.Details.Count != 0)
                    {
                        RegisterChange(change);
                    }
                }

                if (change != null)
                {
                    PostProcessTemplateModuleConfiguratorParameterChange(templateModuleName, configuratorName, change, c.New, c.Original);
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

        protected virtual void PostProcessExternalOAuthServiceChange(Change change, ExternalOAuthServiceTemplateMarkup @new, ExternalOAuthServiceTemplateMarkup original)
        {
        }

        protected virtual void PostProcessTemplateModuleChange(Change change, TemplateModuleTemplateMarkup @new, TemplateModuleTemplateMarkup original)
        {
        }

        protected virtual void PostProcessTemplateModuleConfiguratorChange(string templateModuleName, Change change, TemplateModuleConfiguratorTemplateMarkup @new, TemplateModuleConfiguratorTemplateMarkup original)
        {
        }

        protected virtual void PostProcessTemplateModuleConfiguratorParameterChange(string templateModuleName, string configuratorName, Change change, TemplateModuleConfiguratorParameterTemplateMarkup @new, TemplateModuleConfiguratorParameterTemplateMarkup original)
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
