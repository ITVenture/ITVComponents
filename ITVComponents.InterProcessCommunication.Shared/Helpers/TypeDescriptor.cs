using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.ReflectionHelpers;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    public class TypeDescriptor:IManualSerializer
    {
        private static Dictionary<string, Type> reverseTypes = new Dictionary<string, Type>();

        private string typeName;

        private string fullName;

        private TypeDescriptor[] genericArguments;

        private bool isGeneric;

        [JsonConstructor]
        private TypeDescriptor()
        {

        }

        public static implicit operator Type(TypeDescriptor desc)
        {
            var retVal = Type.GetType(desc.fullName);
            if (retVal == null)
            {
                retVal = Type.GetType(desc.typeName);
            }
            if (retVal == null)
            {
                lock (reverseTypes)
                {
                    if (!reverseTypes.ContainsKey(desc.typeName))
                    {
                        LogEnvironment.LogDebugEvent($"Unkown Type: {desc.typeName}", LogSeverity.Error);
                        throw new TypeLoadException($"Unable to load Type {desc.typeName}");
                    }

                    if (reverseTypes.ContainsKey(desc.typeName))
                    {
                        retVal = reverseTypes[desc.typeName];
                    }
                }
            }

            if (desc.isGeneric)
            {
                var typeArg = (from t in desc.genericArguments select (Type) t).ToArray();
                retVal = retVal.MakeGenericType(typeArg);
            }

            return retVal;
        }

        public static implicit operator TypeDescriptor(Type type)
        {
            var retVal = new TypeDescriptor();
            Type t = type;
            Type[] tparam = new Type[0];
            bool generic = false;
            if (type.IsConstructedGenericType)
            {
                tparam = type.GetGenericArguments();
                t = t.GetGenericTypeDefinition();
                generic = true;
            }

            retVal.typeName = t.AssemblyQualifiedName;
            retVal.fullName = t.FullName;
            retVal.isGeneric = generic;
            retVal.genericArguments = (from n in tparam select (TypeDescriptor)n).ToArray();
            return retVal;
        }

        public static void RegisterReverseType(string assemblyQualifiedName, Type concreteType)
        {
            lock (reverseTypes)
            {
                reverseTypes[assemblyQualifiedName] = concreteType;
            }
        }

        public IList<ManualSerializationData> Data { get; set; }
        public void GetObjectData()
        {
            Data.Add(ManualSerializationData.FromValue(nameof(typeName), typeName));

            Data.Add(ManualSerializationData.FromValue(nameof(fullName), fullName));

            Data.Add(ManualSerializationData.FromValue(nameof(genericArguments), genericArguments));

            Data.Add(ManualSerializationData.FromValue(nameof(isGeneric), isGeneric));
        }

        public void ApplyObjectData()
        {
            typeName = Data.GetDeserializedValue<string>(nameof(typeName));

            fullName = Data.GetDeserializedValue<string>(nameof(fullName));

            genericArguments = Data.GetDeserializedValue<TypeDescriptor[]>(nameof(genericArguments));

            isGeneric = Data.GetDeserializedValue<bool>(nameof(isGeneric));
        }
    }
}
