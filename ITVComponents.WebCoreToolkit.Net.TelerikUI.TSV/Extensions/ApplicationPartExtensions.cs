using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager EnableItvTenantViews<TContext>(this ApplicationPartManager manager) where TContext:DbContext
        {
            var secDefinition = typeof(TContext).GetInterfaces().FirstOrDefault(
                n => n.IsGenericType && n.GetGenericTypeDefinition() == typeof(ISecurityContext<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>));
            var types = secDefinition.GenericTypeArguments;
            var dic = new Dictionary<string, Type>
            {
                { "TUserId", types[0] },
                { "TUser", types[1] },
                { "TRole", types[2] },
                { "TPermission", types[3] },
                { "TUserRole", types[4] },
                { "TRolePermission", types[5] },
                { "TTenantUser", types[6] },
                { "TNavigationMenu", types[7] },
                { "TTenantNavigation", types[8] },
                { "TQuery", types[9] },
                { "TQueryParameter", types[10] },
                { "TTenantQuery", types[11] },
                { "TWidget", types[12] },
                { "TWidgetParam", types[13] },
                { "TUserWidget", types[14] },
                { "TUserProperty", types[15] },
                { "TAssetTemplate", types[16] },
                { "TAssetTemplatePath", types[17] },
                { "TAssetTemplateGrant", types[18] },
                { "TAssetTemplateFeature", types[19] },
                { "TSharedAsset", types[20] },
                { "TSharedAssetUserFilter", types[21] },
                { "TSharedAssetTenantFilter", types[22] },
                {"TClientAppTemplate", types[23] },
                {"TAppPermission", types[24] },
                {"TAppPermissionSet", types[25] },
                {"TClientAppTemplatePermission", types[26] },
                {"TClientApp", types[27] },
                {"TClientAppPermission", types[28] },
                {"TClientAppUser", types[29] },
                { "TContext", typeof(TContext) }
            };

            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic);
            manager.ApplicationParts.Add(part);
            return manager;
        }

        public static ApplicationPartManager EnableItvTenantViews(this ApplicationPartManager manager, Type contextType)
        {
            if (!typeof(DbContext).IsAssignableFrom(contextType))
            {
                throw new InvalidOperationException("contextType must implement DbContext");
            }

            var secDefinition = contextType.GetInterfaces().FirstOrDefault(
                n => n.IsGenericType && n.GetGenericTypeDefinition() == typeof(ISecurityContext<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>));
            var types = secDefinition.GenericTypeArguments;
            var dic = new Dictionary<string, Type>
            {
                { "TUserId", types[0]},
                { "TUser", types[1] },
                { "TRole", types[2] },
                { "TPermission", types[3] },
                { "TUserRole", types[4] },
                { "TRolePermission", types[5] },
                { "TTenantUser", types[6] },
                { "TNavigationMenu", types[7] },
                { "TTenantNavigation", types[8] },
                { "TQuery", types[9]},
                { "TQueryParameter", types[10]},
                { "TTenantQuery", types[11]},
                { "TWidget", types[12]},
                { "TWidgetParam", types[13]},
                { "TUserWidget", types[14]},
                { "TUserProperty", types[15]},
                { "TAssetTemplate", types[16] },
                { "TAssetTemplatePath", types[17] },
                { "TAssetTemplateGrant", types[18] },
                { "TAssetTemplateFeature", types[19] },
                { "TSharedAsset", types[20] },
                { "TSharedAssetUserFilter", types[21] },
                { "TSharedAssetTenantFilter", types[22] },
                {"TClientAppTemplate", types[23] },
                {"TAppPermission", types[24] },
                {"TAppPermissionSet", types[25] },
                {"TClientAppTemplatePermission", types[26] },
                {"TClientApp", types[27] },
                {"TClientAppPermission", types[28] },
                {"TClientAppUser", types[29] },
                { "TContext", contextType}
            };

            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic);
            manager.ApplicationParts.Add(part);
            return manager;
        }
    }
}
