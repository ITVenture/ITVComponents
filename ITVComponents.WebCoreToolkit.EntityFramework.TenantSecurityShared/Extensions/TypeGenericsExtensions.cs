using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class TypeGenericsExtensions
    {
        public static Dictionary<string, Type> GetSecurityContextArguments(this Type type)
        {
            var secDefinition = type.GetInterfaces().FirstOrDefault(
                n => n.IsGenericType && n.GetGenericTypeDefinition() == typeof(ISecurityContext<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>));
            if (secDefinition != null)
            {
                var types = secDefinition.GenericTypeArguments;
                return new Dictionary<string, Type>
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
                    { "TClientAppTemplate", types[23] },
                    { "TAppPermission", types[24] },
                    { "TAppPermissionSet", types[25] },
                    { "TClientAppTemplatePermission", types[26] },
                    { "TClientApp", types[27] },
                    { "TClientAppPermission", types[28] },
                    { "TClientAppUser", types[29] },
                    { "TContext", type }
                };
            }

            throw new InvalidOperationException("Given type does not implement ISecurityContext interface");
        }
    }
}
