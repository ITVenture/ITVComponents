using System.Globalization;
using System.Resources;
using Microsoft.AspNetCore.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Resources
{
    public static class TextsAndMessagesHelper
    {
        private static ResourceManager resourceMan;

        /// <summary>
        ///   Gibt die zwischengespeicherte ResourceManager-Instanz zurück, die von dieser Klasse verwendet wird.
        /// </summary>
        private static ResourceManager ResourceManager => resourceMan ??= new ResourceManager("ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Resources.TextsAndMessages", typeof(TextsAndMessagesHelper).Assembly);

        private static string GetString(string name, CultureInfo culture = null)
        {
            culture ??= CultureInfo.CurrentUICulture;
            var mgr = ResourceManager;
            return mgr.GetString(name, culture);
        }

        public static string IWCN_General_DisplayName
        {
            get { return GetString(nameof(IWCN_General_DisplayName)); }
        }

        public static string IWCN_General_ToggleReordering => GetString(nameof(IWCN_General_ToggleReordering));

        public static string IWCN_PC_Tenant_And_Role_Required
        {
            get { return GetString(nameof(IWCN_PC_Tenant_And_Role_Required)); }
        }

        public static string IWCN_RC_Tenant_And_Permission_Required
        {
            get { return GetString(nameof(IWCN_RC_Tenant_And_Permission_Required)); }
        }
        
        public static string IWCN_RC_Tenant_Bound_User_Required_For_Index => GetString(nameof(IWCN_RC_Tenant_Bound_User_Required_For_Index));
        
        public static string IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole => GetString(nameof(IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole));

        public static string IWCN_TPC_Permission_Or_Role_Selection
        {
            get { return GetString(nameof(IWCN_TPC_Permission_Or_Role_Selection)); }
        }

        public static string IWCN_NV_STDV_No_Tenant_Selected => GetString(nameof(IWCN_NV_STDV_No_Tenant_Selected));
        public static string IWCN_General_TenantTitle => GetString(nameof(IWCN_General_TenantTitle));//"Mandant"

        public static string IWCN_NV_STDV_IconClass => GetString(nameof(IWCN_NV_STDV_IconClass));

        public static string IWCN_General_Name => GetString(nameof(IWCN_General_Name));
        public static string IWCN_General_Description => GetString(nameof(IWCN_General_Description));

        public static string IWCN_P_STDV_Assigned => GetString(nameof(IWCN_P_STDV_Assigned));

        public static string IWCN_P_Tenant_Req_For_Non_Admins => GetString(nameof(IWCN_P_Tenant_Req_For_Non_Admins));
        
        public static string  IWCN_P_Illegal_Permission_Override => GetString(nameof(IWCN_P_Illegal_Permission_Override));
        
        public static string IWCN_P_Sysadmin_Req_To_Del_Global_Perm => GetString(nameof(IWCN_P_Sysadmin_Req_To_Del_Global_Perm));
        
        public static string IWCN_PC_Only_Sysadmin_Can_View_and_Modify_System_Role_Perms => GetString(nameof(IWCN_PC_Only_Sysadmin_Can_View_and_Modify_System_Role_Perms));

        public static string IWCN_General_SystemName => GetString(nameof(IWCN_General_SystemName));

        public static string IWCN_General_TimeZone => GetString(nameof(IWCN_General_TimeZone));

        public static string IWCN_General_Value => GetString(nameof(IWCN_General_Value));
        public static string IWCN_Titles_AuthenticationTypes => GetString(nameof(IWCN_Titles_AuthenticationTypes));
        public static string IWCN_Titles_Navigation => GetString(nameof(IWCN_Titles_Navigation));
        public static string IWCN_Titles_Permissions => GetString(nameof(IWCN_Titles_Permissions));
        public static string IWCN_Titles_Roles => GetString(nameof(IWCN_Titles_Roles));
        public static string IWCN_Titles_Tenants => GetString(nameof(IWCN_Titles_Tenants));
        public static string IWCN_General_ParameterName => GetString(nameof(IWCN_General_ParameterName));
        public static string IWCN_TST_JsonParam => GetString(nameof(IWCN_TST_JsonParam));
        public static string IWCN_Titles_Users => GetString(nameof(IWCN_Titles_Users));
        public static string IWCN_Titles_Properties => GetString(nameof(IWCN_Titles_Properties));
        public static string IWCN_Titles_DiagnosticsQueries => GetString(nameof(IWCN_Titles_DiagnosticsQueries));
        public static string IWCN_Titles_DashboardWidgets => GetString(nameof(IWCN_Titles_DashboardWidgets));

        public static string IWCN_Titles_DbResources => GetString(nameof(IWCN_Titles_DbResources));
        public static string IWCN_Titles_DbCultures => GetString(nameof(IWCN_Titles_DbCultures));

        public static string IWCN_Titles_TenantTypes => GetString(nameof(IWCN_Titles_TenantTypes));

        public static string IWCN_DQ_DbContext => GetString(nameof(IWCN_DQ_DbContext));
        public static string IWCN_DQ_ImplicitReturn => GetString(nameof(IWCN_DQ_ImplicitReturn));
        public static string IWCN_Titles_GlobalSettings => GetString(nameof(IWCN_Titles_GlobalSettings));
        public static string IWCN_Titles_HealthCheckScripts => GetString(nameof(IWCN_Titles_HealthCheckScripts));
        public static string IWCN_DQP_Type => GetString(nameof(IWCN_DQP_Type));

        public static string IWCN_Tenant_Type => GetString(nameof(IWCN_Tenant_Type));
        public static string IWCN_DQP_DateFormat => GetString(nameof(IWCN_DQP_DateFormat));
        public static string IWCN_DPQ_DefaultValue => GetString(nameof(IWCN_DPQ_DefaultValue));
        public static string IWCN_Titles_PlugInConstants => GetString(nameof(IWCN_Titles_PlugInConstants));
        public static string IWCN_TV_Edit_Details => GetString(nameof(IWCN_TV_Edit_Details));
        public static string IWCN_TV_STDV_Assigned => GetString(nameof(IWCN_TV_STDV_Assigned));

        public static string IWCN_Titles_TenantSettings => GetString(nameof(IWCN_Titles_TenantSettings));

        public static string IWCN_Titles_PlugIns => GetString(nameof(IWCN_Titles_PlugIns));
        
        public static string IWCN_General_Is_SystemRole => GetString(nameof(IWCN_General_Is_SystemRole));

        public static string IWCN_Dashboard_DisplayTemplate => GetString(nameof(IWCN_Dashboard_DisplayTemplate));

        public static string IWCN_Titles_TutorialVideos => GetString(nameof(IWCN_Titles_TutorialVideos));

        public static string IWCN_Titles_Services => GetString(nameof(IWCN_Titles_Services));

        public static string IWCN_Language => GetString(nameof(IWCN_Language));

        public static string IWCN_TutorialStreamFormat => GetString(nameof(IWCN_TutorialStreamFormat));

        public static string IWCN_TutorialStreamEncoding => GetString(nameof(IWCN_TutorialStreamEncoding));

        public static string IWCN_Tutorials_SortableName
        {
            get { return GetString(nameof(IWCN_Tutorials_SortableName)); }
        }

        public static string IWCN_Tutorial_DownloadStream => GetString(nameof(IWCN_Tutorial_DownloadStream));

        public static string IWCN_General_IcomingClaimName => GetString(nameof(IWCN_General_IcomingClaimName));
        public static string IWCN_General_IcomingClaimCondition => GetString(nameof(IWCN_General_IcomingClaimCondition));
        public static string IWCN_General_OutgoingClaimName => GetString(nameof(IWCN_General_OutgoingClaimName));
        public static string IWCN_General_OutgoingClaimValue => GetString(nameof(IWCN_General_OutgoingClaimValue));
        public static string IWCN_General_OutgoingIssuer => GetString(nameof(IWCN_General_OutgoingIssuer));
        public static string IWCN_General_OutgoingValueType => GetString(nameof(IWCN_General_OutgoingValueType));
        public static string IWCN_General_OutgoingOriginalIssuer => GetString(nameof(IWCN_General_OutgoingOriginalIssuer));

        public static string IWCN_FTAV_NoStartDate => GetString(nameof(IWCN_FTAV_NoStartDate));

        public static string IWCN_FTAV_NoEndDate => GetString(nameof(IWCN_FTAV_NoEndDate));

        public static string IWCN_Titles_TenantTemplates => GetString(nameof(IWCN_Titles_TenantTemplates));

        public static string IWCN_TT_Apply => GetString(nameof(IWCN_TT_Apply));

        public static string IWCN_TT_Create => GetString(nameof(IWCN_TT_Create));

        public static string IWCN_TT_MetaData => GetString(nameof(IWCN_TT_MetaData));

        public static string IWCN_TT_Template => GetString(nameof(IWCN_TT_Template));

        public static string IWCN_Titles_AssetTemplates => GetString(nameof(IWCN_Titles_AssetTemplates));

        public static string IWCN_AT_SystemKey => GetString(nameof(IWCN_AT_SystemKey));
        public static string IWCN_AT_RequiredFeature => GetString(nameof(IWCN_AT_RequiredFeature));
        public static string IWCN_AT_RequiredPermission => GetString(nameof(IWCN_AT_RequiredPermission));

        public static string IWCN_AT_LegalPaths => GetString(nameof(IWCN_AT_LegalPaths));
        public static string IWCN_AT_SharePermissions => GetString(nameof(IWCN_AT_SharePermissions));
        public static string IWCN_AT_ShareFeatures => GetString(nameof(IWCN_AT_ShareFeatures));

        public static string IWCN_Titles_Sequences => GetString(nameof(IWCN_Titles_Sequences));

        public static string IWCN_DRI_ResetFactory => GetString(nameof(IWCN_DRI_ResetFactory));

        public static string IWCN_DRI_CopyFromOrigin => GetString(nameof(IWCN_DRI_CopyFromOrigin));

        public static string IWCN_ES_Global => GetString(nameof(IWCN_ES_Global));

        public static string IWCN_ES_UniqueName => GetString(nameof(IWCN_ES_UniqueName));

        public static string IWCN_ES_AuthenticationType => GetString(nameof(IWCN_ES_AuthenticationType));

        public static string IWCN_ES_RedirectUri => GetString(nameof(IWCN_ES_RedirectUri));

        public static string IWCN_ES_AuthEndpoint => GetString(nameof(IWCN_ES_AuthEndpoint));

        public static string IWCN_ES_TokenEndpoint => GetString(nameof(IWCN_ES_TokenEndpoint));

        public static string IWCN_ES_RevocationEndpoint => GetString(nameof(IWCN_ES_RevocationEndpoint));

        public static string IWCN_ES_ClientId => GetString(nameof(IWCN_ES_ClientId));

        public static string IWCN_ES_ClientSecret => GetString(nameof(IWCN_ES_ClientSecret));

        public static string IWCN_ES_Scope => GetString(nameof(IWCN_ES_Scope));

        public static string IWCN_ES_ConnectSvc => GetString(nameof(IWCN_ES_ConnectSvc));

        public static string IWCN_ES_UseEncryptPrefix => GetString(nameof(IWCN_ES_UseEncryptPrefix));

        public static string IWCN_ES_EncryptedValue => GetString(nameof(IWCN_ES_EncryptedValue));

        public static string IWCN_ES_DetailsTitle_Params => GetString(nameof(IWCN_ES_DetailsTitle_Params));

        public static string IWCN_ES_DetailsTitle_Test => GetString(nameof(IWCN_ES_DetailsTitle_Test));
        //--

        public static string GetIWCN_General_DisplayName(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_DisplayName), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_PC_Tenant_And_Role_Required(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_PC_Tenant_And_Role_Required), requestCulture.RequestCulture.UICulture); 
        }

        public static string GetIWCN_RC_Tenant_And_Permission_Required(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_RC_Tenant_And_Permission_Required), requestCulture.RequestCulture.UICulture); 
        }
        
        public static string GetIWCN_RC_Tenant_Bound_User_Required_For_Index(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_RC_Tenant_Bound_User_Required_For_Index), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_TPC_Permission_Or_Role_Selection(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_TPC_Permission_Or_Role_Selection), requestCulture.RequestCulture.UICulture); 
        }

        public static string GetIWCN_NV_STDV_No_Tenant_Selected(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_NV_STDV_No_Tenant_Selected), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_General_TenantTitle(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_TenantTitle), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_NV_STDV_IconClass(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_NV_STDV_IconClass), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_General_ToggleReordering(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_ToggleReordering), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_General_Name(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_Name), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_General_Description(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_Description), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_P_STDV_Assigned(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_P_STDV_Assigned), requestCulture.RequestCulture.UICulture);
        }
        
        public static string GetIWCN_P_Tenant_Req_For_Non_Admins(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_P_Tenant_Req_For_Non_Admins), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_P_Illegal_Permission_Override(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_P_Illegal_Permission_Override), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_P_Sysadmin_Req_To_Del_Global_Perm(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_P_Sysadmin_Req_To_Del_Global_Perm), requestCulture.RequestCulture.UICulture);
        }
        
        public static string GetIWCN_PC_Only_Sysadmin_Can_View_and_Modify_System_Role_Perms(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_PC_Only_Sysadmin_Can_View_and_Modify_System_Role_Perms), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_General_SystemName(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_SystemName), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_General_TimeZone(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_General_TimeZone), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_General_Value(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_Value), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_AuthenticationTypes(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_AuthenticationTypes), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_Navigation(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_Navigation), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_Permissions(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_Permissions), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_Roles(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_Roles), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_Tenants(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_Tenants), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_General_ParameterName(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_ParameterName), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_TST_JsonParam(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_TST_JsonParam), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_Users(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_Users), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_Properties(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_Properties), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_DiagnosticsQueries(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_DiagnosticsQueries), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_DashboardWidgets(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_DashboardWidgets), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_DbResources(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_Titles_DbResources), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_Titles_DbCultures(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_Titles_DbCultures), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_Titles_TenantTypes(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_Titles_TenantTypes), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_DQ_DbContext(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_DQ_DbContext), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_DQ_ImplicitReturn(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_DQ_ImplicitReturn), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_GlobalSettings(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_GlobalSettings), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_HealthCheckScripts(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_Titles_HealthCheckScripts),requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_DQP_Type(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_DQP_Type), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Tenant_Type(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Tenant_Type), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_DQP_DateFormat(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_DQP_DateFormat), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_DPQ_DefaultValue(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_DPQ_DefaultValue), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_PlugInConstants(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_PlugInConstants), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_TV_Edit_Details(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_TV_Edit_Details), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_TV_STDV_Assigned(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_TV_STDV_Assigned), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_TenantSettings(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_TenantSettings), requestCulture.RequestCulture.UICulture);
        }
        
        public static string GetIWCN_Titles_PlugIns(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_PlugIns), requestCulture.RequestCulture.UICulture);
        }
        
        public static string GetIWCN_General_Is_SystemRole(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_Is_SystemRole), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_TutorialVideos(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_TutorialVideos), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_Services(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_Titles_Services), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_Tutorials_SortableName(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Tutorials_SortableName), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_TutorialStreamEncoding(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_TutorialStreamEncoding), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_TutorialStreamFormat(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_TutorialStreamFormat), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Language(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Language), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Tutorial_DownloadStream(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Tutorial_DownloadStream), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Dashboard_DisplayTemplate(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Dashboard_DisplayTemplate), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_General_IcomingClaimName(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_IcomingClaimName), requestCulture.RequestCulture.UICulture);
        }
        public static string GetIWCN_General_IcomingClaimCondition(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_IcomingClaimCondition), requestCulture.RequestCulture.UICulture);
        }
        public static string GetIWCN_General_OutgoingClaimName(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_OutgoingClaimName), requestCulture.RequestCulture.UICulture);
        }
        public static string GetIWCN_General_OutgoingClaimValue(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_OutgoingClaimValue), requestCulture.RequestCulture.UICulture);
        }
        public static string GetIWCN_General_OutgoingIssuer(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_OutgoingIssuer), requestCulture.RequestCulture.UICulture);
        }
        public static string GetIWCN_General_OutgoingValueType(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_OutgoingValueType), requestCulture.RequestCulture.UICulture);
        }
        public static string GetIWCN_General_OutgoingOriginalIssuer(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_General_OutgoingOriginalIssuer), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_FTAV_NoStartDate(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_FTAV_NoStartDate), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_FTAV_NoEndDate(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_FTAV_NoEndDate), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_TenantTemplates(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_TenantTemplates), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_TT_Apply(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_TT_Apply), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_TT_Create(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_TT_Create), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_TT_MetaData(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_TT_MetaData), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_TT_Template(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_TT_Template), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_Titles_AssetTemplates(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_AssetTemplates), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_AT_SystemKey(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_AT_SystemKey), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_AT_RequiredFeature(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_AT_RequiredFeature), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_AT_RequiredPermission(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_AT_RequiredPermission), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_AT_LegalPaths(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_AT_LegalPaths), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_AT_SharePermissions(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_AT_SharePermissions), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_AT_ShareFeatures(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_AT_ShareFeatures), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_Titles_Sequences(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_Titles_Sequences), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_DRI_ResetFactory(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_DRI_ResetFactory), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_DRI_CopyFromOrigin(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_DRI_CopyFromOrigin), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_ES_Global(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_Global), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_ES_UniqueName(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_UniqueName), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_ES_AuthenticationType(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_AuthenticationType), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_ES_RedirectUri(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_RedirectUri), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_ES_AuthEndpoint(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_AuthEndpoint), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_ES_TokenEndpoint(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_TokenEndpoint), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_ES_RevocationEndpoint(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_RevocationEndpoint), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_ES_ClientId(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_ClientId), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_ES_ClientSecret(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_ClientSecret), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_ES_Scope(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_Scope), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_ES_ConnectSvc(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_ConnectSvc), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_ES_UseEncryptPrefix(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_ES_UseEncryptPrefix), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_ES_EncryptedValue(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_ES_EncryptedValue), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_ES_DetailsTitle_Params(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_ES_DetailsTitle_Params), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_ES_DetailsTitle_Test(this IRequestCultureFeature requestCulture) =>
            GetString(nameof(IWCN_ES_DetailsTitle_Test), requestCulture.RequestCulture.UICulture);
    }
}
