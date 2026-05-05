using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class MethodHelper
    {
        public static TMethod GetMethod<TMethod>(this Type staticClass, Type contextType, string methodName)
            where TMethod : Delegate
        {
            var scb = typeof(ISecurityContext<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>);
            var ifs = contextType.GetInterfaces().FirstOrDefault(n =>
                n.IsGenericType && n.GetGenericTypeDefinition() == scb);
            if (ifs != null)
            {
                //var rawTypes = new[] { contextType }.Concat(ifs.GetGenericArguments()).ToArray();
                var rawTypes = ifs.GetSecurityContextArguments();
                var meth = staticClass.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(n => n.IsGenericMethodDefinition && n.Name == methodName)
                    .Select(m => new {raw = m, finalized = TryFinalizeMethod(m, rawTypes)})
                    .FirstOrDefault(n => n.finalized != null);
                if (meth != null)
                {
                    var impl = meth.finalized;
                    var fx = impl.CreateDelegate<TMethod>();
                    return fx;
                }
            }

            return null;
        }

        private static MethodInfo TryFinalizeMethod(MethodInfo method, Dictionary<string, Type> typeArguments)
        {
            var arg = method.GetGenericArguments();
            var nt = new Type[arg.Length];
            var success = true;
            var defaultArgs = Attribute.GetCustomAttributes(method).Where(n => n is CustomGenericTypeArgAttribute).Cast<CustomGenericTypeArgAttribute>().ToArray();
            for (var index = 0; index < arg.Length; index++)
            {
                var t = arg[index];
                if (typeArguments.ContainsKey(t.Name))
                {
                    nt[index] = typeArguments[t.Name];
                }
                else if (defaultArgs.Any(n => n.Name == t.Name))
                {
                    nt[index] = defaultArgs.First(n => n.Name == t.Name).Type;
                }
                else
                {
                    LogEnvironment.LogEvent($"Missing argument {t.Name}.", LogSeverity.Warning);
                    success = false;
                    break;
                }
            }

            if (success)
            {
                try
                {
                    var gn = method.MakeGenericMethod(nt);
                    return gn;
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Failed to create generic Type: {ex.Message}.", LogSeverity.Warning);
                }
            }

            return null;
        }
    }
}
