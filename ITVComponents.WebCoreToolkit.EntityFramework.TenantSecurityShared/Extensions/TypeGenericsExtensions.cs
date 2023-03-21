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
                n => n.IsGenericType && n.GetGenericTypeDefinition() == typeof(ISecurityContext<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>));
            if (secDefinition != null)
            {
                var types = secDefinition.GenericTypeArguments;
                var genTypes = secDefinition.GetGenericTypeDefinition().GetGenericArguments();
                var dic = new Dictionary<string, Type>();
                for (int i = 0; i < types.Length; i++)
                {
                    dic.Add(genTypes[i].Name, types[i]);
                }

                dic.Add("TContext", type);
                return dic;
                /*return new Dictionary<string, Type>
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
                    {"TWidgetLocalization", types[14]},
                    { "TUserWidget", types[15] },
                    { "TUserProperty", types[16] },
                    { "TAssetTemplate", types[17] },
                    { "TAssetTemplatePath", types[18] },
                    { "TAssetTemplateGrant", types[19] },
                    { "TAssetTemplateFeature", types[20] },
                    { "TSharedAsset", types[21] },
                    { "TSharedAssetUserFilter", types[22] },
                    { "TSharedAssetTenantFilter", types[23] },
                    { "TClientAppTemplate", types[24] },
                    { "TAppPermission", types[25] },
                    { "TAppPermissionSet", types[26] },
                    { "TClientAppTemplatePermission", types[27] },
                    { "TClientApp", types[28] },
                    { "TClientAppPermission", types[29] },
                    { "TClientAppUser", types[30] },
                    { "TContext", type }
                };*/
            }

            throw new InvalidOperationException("Given type does not implement ISecurityContext interface");
        }
    }
}
