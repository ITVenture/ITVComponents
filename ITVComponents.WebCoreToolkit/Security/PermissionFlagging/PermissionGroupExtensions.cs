using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security.PermissionFlagging.Models;

namespace ITVComponents.WebCoreToolkit.Security.PermissionFlagging
{
    public static class PermissionGroupExtensions
    {
        public static PermissionFlag[] GetPermissionGroupName(this Type type, bool withGeneral = true)
        {
            PermissionFlag[] retVal = [];
            if (Attribute.IsDefined(type, typeof(DisableFilterPermissionGroupAttribute), true))
            {
                var att = Attribute.GetCustomAttributes(type,
                    typeof(DisableFilterPermissionGroupAttribute), true).Cast<DisableFilterPermissionGroupAttribute> ().ToArray();
                var tmpRet = (from t in att
                    group t by t.PermissionCategory into g
                    select new PermissionFlag
                    {
                        PermissionGroups = g.SelectMany(n=>n.PermissionGroupName).Distinct().ToArray(),
                        Category = g.Key
                    }).ToArray();
                if (withGeneral && tmpRet.All(n => n.Category != "General"))
                {
                    tmpRet =
                    [
                        new PermissionFlag
                        {
                            PermissionGroups = ["Default"],
                            Category = "General"
                        },
                        ..tmpRet
                    ];
                }
                         
                retVal = tmpRet;
            }
            else if (withGeneral)
            {
                retVal =
                [
                    new PermissionFlag
                    {
                        PermissionGroups = ["Default"],
                        Category = "General"
                    }
                ];
            }

            return retVal;
        }

        public static PermissionFlag[] GetPermissionGroupName(this PropertyInfo prop, Type genericBaseType = null)
        {
            PermissionFlag[] onProp = [];
            if (Attribute.IsDefined(prop, typeof(DisableFilterPermissionGroupAttribute), true))
            {
                var att = Attribute.GetCustomAttributes(prop,
                    typeof(DisableFilterPermissionGroupAttribute), true).Cast<DisableFilterPermissionGroupAttribute>().ToArray();
                onProp = (from t in att
                    group t by t.PermissionCategory into g
                    select new PermissionFlag
                    {
                        PermissionGroups = g.SelectMany(n => n.PermissionGroupName).Distinct().ToArray(),
                        Category = g.Key
                    }).ToArray();
                /*var att = (DisableFilterPermissionGroupAttribute)Attribute.GetCustomAttribute(prop,
                    typeof(DisableFilterPermissionGroupAttribute), true);
                return att.PermissionGroupName;*/
            }

            PermissionFlag[] onBase = [];
            if (genericBaseType != null && prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == genericBaseType)
            {
                var targetType = prop.PropertyType.GetGenericArguments().First();
                onBase = targetType.GetPermissionGroupName(false);
            }

            PermissionFlag[] onRoot = prop.PropertyType.GetPermissionGroupName();
            string[] oj = ((string[])[..onBase.Select(n=>n.Category), ..onRoot.Select(n => n.Category)]).Distinct().ToArray();
            var baseTopics = (from o in oj
                join r in onRoot on o equals r.Category into or
                                from nor in or.DefaultIfEmpty()
                join b in onBase on o equals b.Category into ob
                                from nob in ob.DefaultIfEmpty()
                             select nob ?? nor).ToArray();
            oj = ((string[])[.. baseTopics.Select(n => n.Category), .. onProp.Select(n => n.Category)]).Distinct().ToArray();
            return (from o in oj
                join p in onProp on o equals p.Category into op
                                from nop in op.DefaultIfEmpty()
                join b in baseTopics on o equals b.Category into ob
                                from nob in ob.DefaultIfEmpty()
                select nop ?? nob).ToArray();
        }

        public static (string category, string topic)[] FilteredTopic(this PermissionFlag[] flags, string category)
        {
            return (from t in flags
                    where t.Category == category
                    select t.PermissionGroups.Select(n => (category: t.Category, topic: n))).SelectMany(n => n)
                .ToArray();
        }
    }
}
