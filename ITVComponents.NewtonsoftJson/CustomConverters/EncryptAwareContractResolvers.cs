using System.Reflection;
using ITVComponents.Json.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ITVComponents.NewtonsoftJson.CustomConverters
{
    internal static class EncryptContractHelper
    {
        public static void ApplyEncryptConverter(JsonProperty property, MemberInfo member)
        {
            // Don't clobber an explicit [JsonConverter]; only plain string members carrying the
            // strategy-neutral [EncryptJsonValue] marker get the encrypt converter.
            if (property.PropertyType != typeof(string) || property.Converter != null)
            {
                return;
            }

            var enc = member.GetCustomAttribute<EncryptJsonValueAttribute>(true);
            if (enc != null)
            {
                property.Converter = string.IsNullOrEmpty(enc.Entropy)
                    ? new JsonStringEncryptConverter()
                    : new JsonStringEncryptConverter(enc.Entropy);
            }
        }
    }

    internal class EncryptAwareContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            EncryptContractHelper.ApplyEncryptConverter(property, member);
            return property;
        }
    }

    internal class EncryptAwareCamelCaseContractResolver : CamelCasePropertyNamesContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            EncryptContractHelper.ApplyEncryptConverter(property, member);
            return property;
        }
    }
}
