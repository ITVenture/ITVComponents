namespace ITVComponents.WebCoreToolkit
{
    public static class Global
    {
        public const string AuthenticatorName = "authenticator";

        public const string RoleVerifyerName = "roleVerifyer";

        public const string ServerScriptExecutorName = "serverScriptRunner";

        public const string SessionProfilerPluginName = "sessionProfiler";

        public const string ServiceProviderName = "serviceProvider";

        public const string PlugInSelectorName = "plugInProvider";

        public const string SecurityAccessProvider = "securityAccessProvider";

        public const string TenantObjectCacheName = "residentObjectCache";

        public const string FixedAssetRequestQueryParameter = "SharedAssetKey";

        /// <summary>
        /// Query-parameter carrying the anonymous access-token in the deprecated query form. Kept readable
        /// so links that were already sent out keep working; new links use the path form.
        /// </summary>
        public const string FixedAssetTokenQueryParameter = "__AccessToken";

        /// <summary>
        /// Marks the first path segment as a shared-asset segment (<c>/~{key}[.{token}]/…</c>). A segment
        /// starting with this is an asset segment and nothing else, which is what makes it distinguishable
        /// from any page path.
        /// </summary>
        public const string SharedAssetPathMarker = "~";

        /// <summary>
        /// Key under which the raw asset segment (with marker) is stored in <c>HttpContext.Items</c>.
        /// </summary>
        public const string SharedAssetSegmentItemKey = "ITVComponents.WebCoreToolkit.SharedAssetSegment";

        /// <summary>
        /// Key under which the decoded asset key is stored in <c>HttpContext.Items</c>.
        /// </summary>
        public const string SharedAssetKeyItemKey = "ITVComponents.WebCoreToolkit.SharedAssetKey";

        /// <summary>
        /// Key under which the anonymous access-token of the current request is stored in
        /// <c>HttpContext.Items</c>.
        /// </summary>
        public const string SharedAssetTokenItemKey = "ITVComponents.WebCoreToolkit.SharedAssetToken";

        /// <summary>
        /// Key under which the tenant of an ad-hoc ticket is stored in <c>HttpContext.Items</c>.
        /// </summary>
        public const string SharedAssetTicketTenantItemKey = "ITVComponents.WebCoreToolkit.SharedAssetTicketTenant";

        /// <summary>
        /// Key under which the encrypted payload of an ad-hoc ticket is stored in <c>HttpContext.Items</c>.
        /// </summary>
        public const string SharedAssetTicketPayloadItemKey = "ITVComponents.WebCoreToolkit.SharedAssetTicketPayload";


        public const string AppUserKeyIndicatorFormat = "##APPUSER##{0}#";

        public const string AppUserKeyPattern = "^##APPUSER##(?<appUserKey>[^#]+)#$";

        public const string PartTypeLoadBehaviorOption = "PartTypeLoadBehavior";
    }
}
