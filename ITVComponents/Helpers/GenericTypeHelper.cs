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
            foreach (var n in t)
            {
                var p = n.GetGenericArguments();
                var u = knownParameters.FirstOrDefault(i => p.All(pm => i.Value.ContainsKey(pm.Name))).Value;
                if (u == null)
                {
                    // Ein Suchlauf, kein Auftrag: diese Methode gehoert nicht zu den bekannten Parametern,
                    // und das ist der Normalfall (der Typ hat auch generische Methoden zu anderen Zwecken).
                    // Trotzdem nicht stumm - genau hier verschwindet sonst eine Registrierung, weil jemand
                    // einen Typparameter umbenannt hat, und niemand erfaehrt es.
                    LogEnvironment.LogDebugEvent(
                        $"'{staticType.FullName}.{n.Name}' was skipped: no known parameter set covers all of "
                        + $"[{string.Join(", ", p.Select(pm => pm.Name))}]. The binding goes by the NAME of "
                        + "the type parameter - a renamed one looks exactly like this.",
                        LogSeverity.Report);
                    continue;
                }

                var mth = (from m in p join a in u on m.Name equals a.Key select a.Value).ToArray();
                MethodInfo retMi = null;
                try
                {
                    retMi = n.MakeGenericMethod(mth);
                }
                catch (Exception ex)
                {
                    // Hier waren alle Namen bekannt - dass es TROTZDEM scheitert, ist kein Normalfall:
                    // die Bedingungen der Methode passen nicht zum Kontext. Das gehoert ins Log, wo man
                    // es findet, und nicht nur in die Debug-Ausgabe.
                    LogEnvironment.LogEvent(
                        $"'{staticType.FullName}.{n.Name}' could not be built although every type parameter "
                        + $"was known - it will NOT be available. {ex.OutlineException()}",
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
