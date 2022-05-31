using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class MethodHelper
    {
        public static TMethod GetMethod<TMethod>(this Type staticClass, Type contextType, string methodName) 
            where TMethod : Delegate
        {
            var scb = typeof(ISecurityContext<,,,,,,,,,,,,,,,,,,,,,,>);
            var ifs = contextType.GetInterfaces().FirstOrDefault(n =>
                n.IsGenericType && n.GetGenericTypeDefinition() == scb);
            if (ifs != null)
            {
                var rawTypes = new[] { contextType }.Union(ifs.GetGenericArguments()).ToArray();
                var meth = staticClass.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(n => n.IsGenericMethodDefinition && n.Name == methodName &&
                                         n.GetGenericArguments().Length == rawTypes.Length);
                if (meth != null)
                {
#if NET5_0_OR_GREATER
                    var impl = meth.MakeGenericMethod(rawTypes);
                    var fx = impl.CreateDelegate<TMethod>();
                    return fx;
#else
                    var impl = meth.MakeGenericMethod(rawTypes);
                    var fx = impl.CreateDelegate(typeof(TMethod));
                    return (TMethod)fx;
#endif
                }
            }

            return null;
        }
    }
}
