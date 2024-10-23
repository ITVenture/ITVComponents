using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.Helpers
{
    public static class GenericTypeHelper
    {
        public static IEnumerable<MethodInfo> ImplementGenericMethods(this Type staticType,
            Dictionary<Type, Dictionary<string, Type>> knownParameters,
            BindingFlags methodFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod)
        {
            var t = staticType
                .GetMethods(methodFlags)
                .Where(n => n.IsGenericMethod);
            //var p = t.GetGenericArguments();
            var p2 = (from n in t
                let p = n.GetGenericArguments()
                let u = knownParameters.FirstOrDefault(i => p.All(pm => i.Value.ContainsKey(pm.Name))).Value
                where u != null
                select new { n, p, u });
            foreach (var i in p2)
            {
                var mth = (from m in i.p join a in i.u on m.Name equals a.Key select a.Value).ToArray();
                MethodInfo retMi = null;
                try
                {
                    retMi = i.n.MakeGenericMethod(mth);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent($"Failed to construct method {i.n.Name}. {ex.OutlineException()}",
                        LogSeverity.Error);
                }

                if (retMi != null)
                {
                    yield return retMi;
                }
            }
        }

        public static IEnumerable<MethodInfo> ImplementGenericMethods(this Type type,
            Type staticType,
            Type interfaceType = null,
            BindingFlags methodFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
            params (string name, Type type)[] fixTypeEntries)
        {
            var tmp = type.GetInterfaceGenericArgumentsOf(interfaceType, fixTypeEntries);
            return ImplementGenericMethods(staticType, methodFlags: methodFlags, knownParameters: tmp);
        }

        public static Type FinalizeType(this Type type,
            Type genericType,
            Type interfaceType = null,
            params (string name, Type type)[] fixTypeEntries)
        {
            var tmp = type.GetInterfaceGenericArgumentsOf(interfaceType, fixTypeEntries);
            return FinalizeType(genericType, tmp);
        }

        public static GenericTypeArgument[] GetGenericTypeArguments(this Type genericType)
        {
            var p = genericType.GetGenericArguments();
            return p.Select(t => new GenericTypeArgument { GenericTypeName = t.Name }).ToArray();
        }

        public static bool FinalizeTypeArguments(this IList<GenericTypeArgument> arguments,
            Dictionary<Type, Dictionary<string, Type>> knownParameters)
        {
            var accurateProvider = knownParameters
                .FirstOrDefault(i => arguments.All(pm => i.Value.ContainsKey(pm.GenericTypeName))).Value;
            return FinalizeTypeArguments(arguments, accurateProvider);
        }

        public static bool FinalizeTypeArguments(this IList<GenericTypeArgument> arguments,
            Dictionary<string, Type> knownTypes)
        {
            if (knownTypes != null && knownTypes.Count >= arguments.Count)
            {
                var p2 = (from a in arguments
                    join t in knownTypes
                        on a.GenericTypeName equals t.Key
                    select new { a, t.Value }).ToArray();
                if (p2.Length == arguments.Count)
                {
                    foreach (var item in p2)
                    {
                        item.a.TypeResult = item.Value;
                    }

                    return true;
                }
            }

            return false;
        }

        public static Type FinalizeType(this Type genericType, Dictionary<Type, Dictionary<string, Type>> knownParameters)
        {
            if (genericType == null || !genericType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Generic Type Definition required", nameof(genericType));
            }

            var p = genericType.GetGenericTypeArguments();
            if (p.FinalizeTypeArguments(knownParameters))
            {
                return genericType.MakeGenericType(p.Select(n=>n.TypeResult).ToArray());
            }

            throw new InvalidOperationException("Unable to implement Generic Type with the given type-information");
        }

        public static Type FinalizeType(this Type genericType, Dictionary<string, Type> knownParameters)
        {
            if (genericType == null || !genericType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Generic Type Definition required", nameof(genericType));
            }

            var p = genericType.GetGenericTypeArguments();
            if (p.FinalizeTypeArguments(knownParameters))
            {
                return genericType.MakeGenericType(p.Select(n => n.TypeResult).ToArray());
            }

            throw new InvalidOperationException("Unable to implement Generic Type with the given type-information");
        }

        public static Dictionary<Type,Dictionary<string, Type>> GetInterfaceGenericArgumentsOf(this Type type, Type interfaceType = null,
            params (string name, Type type)[] fixTypeEntries)
        {
            if (interfaceType != null && !interfaceType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Generic Type Definition required", nameof(interfaceType));
            }

            var retVal = new Dictionary<Type, Dictionary<string, Type>>();
            var definitions = type.GetInterfaces().Where(
                n => n.IsGenericType && (interfaceType == null || n.GetGenericTypeDefinition() == interfaceType));
            foreach (var def in definitions)
            {
                var types = def.GenericTypeArguments;
                var genDef = def.GetGenericTypeDefinition();
                var genTypes = genDef.GetGenericArguments();
                var dic = new Dictionary<string, Type>();
                for (int i = 0; i < types.Length; i++)
                {
                    dic.Add(genTypes[i].Name, types[i]);
                }

                foreach (var fixTypeEntry in fixTypeEntries)
                {
                    dic.Add(fixTypeEntry.name, fixTypeEntry.type);
                }

                retVal.Add(genDef, dic);
            }

            if (retVal.Count == 0)
            {
                var dic = new Dictionary<string, Type>();
                foreach (var fixTypeEntry in fixTypeEntries)
                {
                    dic.Add(fixTypeEntry.name, fixTypeEntry.type);
                }

                retVal.Add(type, dic);
            }

            return retVal;
        }
    }
}
