#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.Plugins.Modules
{
    /// <summary>
    /// Recognizes contract assemblies: those every module shares with its host instead of bringing its own copy - the
    /// ITVComponents packages and the abstractions of an application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An assembly is a contract if it carries <c>[assembly: AssemblyMetadata("ITVComponents.PluginContract", "true")]</c>.
    /// The ITVComponents packages carry it (set in <c>Directory.Build.targets</c>); an application marks its own
    /// abstractions the same way. The attribute is a framework type, so even an assembly without any dependency can carry
    /// it.
    /// </para>
    /// <para>
    /// It is read from the metadata, without loading the assembly.
    /// </para>
    /// </remarks>
    public static class PluginContract
    {
        /// <summary>
        /// The key of the <c>AssemblyMetadata</c> attribute that marks a contract assembly.
        /// </summary>
        public const string MetadataKey = "ITVComponents.PluginContract";

        private static readonly ConcurrentDictionary<string, bool> known = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates whether the assembly in a file is a contract assembly.
        /// </summary>
        /// <param name="path">the assembly file</param>
        /// <returns>true if it carries the contract mark; false if not, or if the file is no readable assembly</returns>
        public static bool IsContract(string path)
        {
            return known.GetOrAdd(Path.GetFullPath(path), Read);
        }

        private static bool Read(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var pe = new PEReader(stream);
                if (!pe.HasMetadata)
                {
                    return false;
                }

                var metadata = pe.GetMetadataReader();
                foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
                {
                    var attribute = metadata.GetCustomAttribute(handle);
                    if (!IsAssemblyMetadataAttribute(metadata, attribute))
                    {
                        continue;
                    }

                    var value = attribute.DecodeValue(new StringOnlyDecoder());
                    if (value.FixedArguments.Length == 2
                        && string.Equals(value.FixedArguments[0].Value as string, MetadataKey, StringComparison.Ordinal)
                        && string.Equals(value.FixedArguments[1].Value as string, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is InvalidOperationException || ex is IOException)
            {
                LogEnvironment.LogDebugEvent($"{path} is no readable assembly; it is not a contract ({ex.Message}).", LogSeverity.Report);
                return false;
            }
        }

        private static bool IsAssemblyMetadataAttribute(MetadataReader metadata, CustomAttribute attribute)
        {
            StringHandle name;
            StringHandle ns;
            switch (attribute.Constructor.Kind)
            {
                case HandleKind.MemberReference:
                    var member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    if (member.Parent.Kind != HandleKind.TypeReference)
                    {
                        return false;
                    }

                    var reference = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                    name = reference.Name;
                    ns = reference.Namespace;
                    break;
                case HandleKind.MethodDefinition:
                    var method = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                    var definition = metadata.GetTypeDefinition(method.GetDeclaringType());
                    name = definition.Name;
                    ns = definition.Namespace;
                    break;
                default:
                    return false;
            }

            return metadata.StringComparer.Equals(name, "AssemblyMetadataAttribute") && metadata.StringComparer.Equals(ns, "System.Reflection");
        }

        /// <summary>
        /// Decodes the two string arguments of <c>AssemblyMetadataAttribute</c>; nothing else is needed.
        /// </summary>
        private sealed class StringOnlyDecoder : ICustomAttributeTypeProvider<object?>
        {
            public object? GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode;

            public object? GetSystemType() => typeof(Type);

            public object? GetSZArrayType(object? elementType) => null;

            public object? GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => null;

            public object? GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => null;

            public object? GetTypeFromSerializedName(string name) => null;

            public PrimitiveTypeCode GetUnderlyingEnumType(object? type) => PrimitiveTypeCode.Int32;

            public bool IsSystemType(object? type) => type is Type;
        }
    }
}
