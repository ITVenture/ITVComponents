using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.Json.Contracts
{
    /// <summary>
    /// Polymorphism configured at runtime (<see cref="ConfigureType"/>): a base type gets the registered derived types
    /// with the discriminator property <c>$$Type</c>. Types that are not configured are left to the next resolver of the
    /// chain.
    /// </summary>
    public class DynamicContractResolver:DefaultJsonTypeInfoResolver
    {
        private static ConcurrentDictionary<Type, ConcurrentDictionary<string, JsonDerivedType>> typeMap = new ConcurrentDictionary<Type, ConcurrentDictionary<string, JsonDerivedType>>();
        private static ConcurrentDictionary<Type, Type> backwardMap = new ConcurrentDictionary<Type, Type>();

        public static void ConfigureType(Type baseType, Type derivedType, string discriminator)
        {
            typeMap.GetOrAdd(baseType, t => new ConcurrentDictionary<string, JsonDerivedType>())
                .TryAdd(discriminator, new JsonDerivedType(derivedType, discriminator));
            backwardMap.TryAdd(derivedType, baseType);
        }

        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var redirected = backwardMap.TryGetValue(type, out var retp);
            var targetType = !redirected ? type : retp;
            var retVal = base.GetTypeInfo(type, options);
            var baseInfo = targetType != type ? GetTypeInfo(targetType, options) : null;
            if (baseInfo == null && typeMap.TryGetValue(targetType, out var detailMap))
            {
                var tp = (retVal.PolymorphismOptions??=new JsonPolymorphismOptions(){TypeDiscriminatorPropertyName = "$$Type"}).DerivedTypes?.ToArray();
                var d = detailMap.Values.Where(i => tp.All(o => o.TypeDiscriminator != i.TypeDiscriminator));
                foreach (var jsonDerivedType in d)
                {
                    retVal.PolymorphismOptions.DerivedTypes.Add(jsonDerivedType);
                }
            }
            else if (baseInfo != null)
            {
                retVal.PolymorphismOptions = new JsonPolymorphismOptions{TypeDiscriminatorPropertyName = "$$Type"};
                foreach (var poly in baseInfo.PolymorphismOptions.DerivedTypes)
                {
                    if (type.IsAssignableFrom(poly.DerivedType))
                    {
                        retVal.PolymorphismOptions.DerivedTypes.Add(poly);
                    }
                }
            }
            else
            {
                // Not configured here: the next resolver of the chain describes the type.
                LogEnvironment.LogDebugEvent(
                    $"No custom mapping info found for Type '{type.AssemblyQualifiedName}'.", LogSeverity.Report);
                retVal = null;
            }

            return retVal;
        }
    }
}
