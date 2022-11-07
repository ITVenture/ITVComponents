using ITVComponents.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.TypeConversion;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ModuleConfigHandling
{
    public static class ModuleConfigHandlerHelper
    {
        public static void InvokeHandler(object handler, HandlerMethodName methodName,
            IDictionary<string, object> arguments)
        {
            InvokeHandler<object>(handler, methodName, arguments);
        }

        public static T InvokeHandler<T>(object handler, HandlerMethodName methodName, IDictionary<string, object> arguments)
        {
            var t = handler.GetType();
            var method = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod)
                .FirstOrDefault(n => Attribute.IsDefined(n, typeof(HandlerMethodAttribute)) && ((HandlerMethodAttribute)Attribute.GetCustomAttribute(t, typeof(HandlerMethodAttribute))).Name == methodName);
            method ??= t.GetMethod(methodName.ToString(),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
            if (method != null)
            {
                var parameters = method.GetParameters();
                var methArg = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    var p = parameters[i];
                    if (arguments.TryGetValue(p.Name, out var raw))
                    {
                        if (TypeConverter.TryConvert(raw, p.ParameterType, out var converted))
                        {
                            methArg[i] = converted;
                        }
                        else if (raw is string js)
                        {
                            methArg[i] = JsonHelper.FromJsonString(p.ParameterType, js);
                        }
                        else if (p.HasDefaultValue)
                        {
                            methArg[i] = p.DefaultValue;
                        }
                        else
                        {
                            throw new InvalidOperationException($"No appropriate value found for parameter {p.Name}");
                        }
                    }
                }

                return (T)method.Invoke(handler, methArg);
            }

            throw new InvalidOperationException($"Method {methodName} was not found on the target handler");
        }
    }
}
