#nullable enable
// This file is compiled twice: into ITVComponents.Plugins (public), and as a linked source into
// ITVComponents.UpdateService.ModuleBuild (internal, ITV_MODULEBUILD). It must therefore stay free of dependencies
// beyond System.Reflection.Metadata, compile against netstandard2.0, and contain no logging: every finding is
// returned to the caller, which reports it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using ITVComponents.Plugins.Initialization;

#if ITV_MODULEBUILD
namespace ITVComponents.Plugins.Initialization
{
    /// <summary>
    /// Mirror of <c>ITVComponents.Plugins.Initialization.PluginLoadType</c> for the module build, which does not
    /// reference ITVComponents.Plugins. The values must stay the same.
    /// </summary>
    internal enum PluginLoadType
    {
        Singleton = 0,
        ScopeStartup = 1,
        Scope = 2
    }
}
#endif

namespace ITVComponents.Plugins.Modules
{
    /// <summary>
    /// Whether a plugin of a module exists exactly once or is a template for instances.
    /// </summary>
    /// <remarks>The values are read from the metadata as numbers - never renumber them.</remarks>
#if ITV_MODULEBUILD
    internal
#else
    public
#endif
    enum ModulePluginKind
    {
        /// <summary>
        /// Exactly one instance, with the name fixed by the module. The update service creates it together with the
        /// module.
        /// </summary>
        Infrastructure = 0,

        /// <summary>
        /// Zero to any number of instances, each with a name given when it is created.
        /// </summary>
        Template = 1
    }

    /// <summary>
    /// A <c>ModulePluginAttribute</c> as read from the metadata of a module assembly.
    /// </summary>
#if ITV_MODULEBUILD
    internal
#else
    public
#endif
    sealed class ModulePluginMark
    {
        internal ModulePluginMark(string assemblyFile, string typeName, ModulePluginKind kind, PluginLoadType loadType, string? name, string? displayName,
            bool isOpenGeneric, bool isAbstract, bool? implementsPlugin, bool? hasDeviceSettings, string? unverifiedReason)
        {
            HasDeviceSettings = hasDeviceSettings;
            AssemblyFile = assemblyFile;
            TypeName = typeName;
            Kind = kind;
            LoadType = loadType;
            Name = name;
            DisplayName = displayName;
            IsOpenGeneric = isOpenGeneric;
            IsAbstract = isAbstract;
            ImplementsPlugin = implementsPlugin;
            UnverifiedReason = unverifiedReason;
        }

        /// <summary>The file name of the assembly that declares the type.</summary>
        public string AssemblyFile { get; }

        /// <summary>The full name of the type; nested types with '+'.</summary>
        public string TypeName { get; }

        /// <summary>The kind as given in the mark; may be undefined if the mark was built against another version.</summary>
        public ModulePluginKind Kind { get; }

        /// <summary>The load type as given in the mark; may be undefined if the mark was built against another version.</summary>
        public PluginLoadType LoadType { get; }

        /// <summary>The fixed name (infrastructure only).</summary>
        public string? Name { get; }

        /// <summary>The name shown when the plugin is offered.</summary>
        public string? DisplayName { get; }

        /// <summary>The type has open type parameters.</summary>
        public bool IsOpenGeneric { get; }

        /// <summary>The type is abstract (or an interface) and can not be built.</summary>
        public bool IsAbstract { get; }

        /// <summary>
        /// Whether the type implements <c>ITVComponents.Plugins.IPlugin</c>; <c>null</c> if that could not be decided
        /// because a part of its type hierarchy was not found (see <see cref="UnverifiedReason"/>).
        /// </summary>
        public bool? ImplementsPlugin { get; }

        /// <summary>
        /// Whether the type implements <c>ITVComponents.AgentServices.Abstractions.IHasDeviceSettings&lt;T&gt;</c>: it then
        /// gets its settings in the constructor (<c>$DeviceSettings</c>). <c>null</c> if that could not be decided.
        /// </summary>
        public bool? HasDeviceSettings { get; }

        /// <summary>Why <see cref="ImplementsPlugin"/> or <see cref="HasDeviceSettings"/> could not be decided.</summary>
        public string? UnverifiedReason { get; }

        /// <inheritdoc />
        public override string ToString() => $"{TypeName} in {AssemblyFile} ({Kind}, {LoadType}{(Name != null ? ", name " + Name : string.Empty)})";
    }

    /// <summary>
    /// Reads and checks the <c>ModulePluginAttribute</c> marks of a module from the metadata - without loading any
    /// code of the module. Used by the module build (<c>ModulePackager</c>) and by the update service, so both apply
    /// the same rules.
    /// </summary>
#if ITV_MODULEBUILD
    internal
#else
    public
#endif
    static class ModulePluginMarks
    {
        /// <summary>The namespace of the mark.</summary>
        public const string AttributeNamespace = "ITVComponents.Plugins.Modules";

        /// <summary>The type name of the mark.</summary>
        public const string AttributeName = "ModulePluginAttribute";

        /// <summary>Every plugin implements it.</summary>
        private const string PluginInterface = "ITVComponents.Plugins.IPlugin";

        /// <summary>
        /// The marker of a device with settings (ITVComponents.AgentServices.Abstractions). Named, not referenced: the
        /// plugin abstractions do not depend on the agent services.
        /// </summary>
        private const string DeviceSettingsInterface = "ITVComponents.AgentServices.Abstractions.IHasDeviceSettings`1";

        private const int MaxDepth = 64;

        private static readonly Regex InstanceNamePattern = new Regex("^[A-Za-z][A-Za-z0-9_.-]{0,63}$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Whether a name may be used for a plugin instance of a module: starts with a letter, then letters, digits,
        /// '_', '.' or '-', 64 characters at most.
        /// </summary>
        /// <remarks>
        /// Stricter than a device name in the agent settings (§72: no ':'), because the name ends up in
        /// <c>plugins.json</c>, in the settings path <c>Devices:&lt;Name&gt;</c> and in log messages.
        /// </remarks>
        public static bool IsValidInstanceName(string? name) => name != null && InstanceNamePattern.IsMatch(name);

        /// <summary>
        /// Reads the marks of one assembly.
        /// </summary>
        /// <param name="assemblyPath">the assembly file</param>
        /// <param name="resolveAssembly">finds the file of a referenced assembly by its simple name, to follow base
        /// types and interfaces into it; <c>null</c> if it is not available. Framework assemblies are never asked for.</param>
        /// <returns>the marks; empty if the file is no managed assembly or carries none</returns>
        /// <exception cref="BadImageFormatException">the file is damaged</exception>
        public static IReadOnlyList<ModulePluginMark> Read(string assemblyPath, Func<string, string?>? resolveAssembly)
        {
            using var scope = new MetadataScope(resolveAssembly);
            var metadata = scope.OpenRoot(assemblyPath);
            if (metadata == null)
            {
                return Array.Empty<ModulePluginMark>();
            }

            var file = Path.GetFileName(assemblyPath);
            var marks = new List<ModulePluginMark>();
            foreach (var handle in metadata.TypeDefinitions)
            {
                var type = metadata.GetTypeDefinition(handle);
                foreach (var attributeHandle in type.GetCustomAttributes())
                {
                    var attribute = metadata.GetCustomAttribute(attributeHandle);
                    if (!IsAttribute(metadata, attribute, AttributeNamespace, AttributeName))
                    {
                        continue;
                    }

                    var value = attribute.DecodeValue(new MarkDecoder());
                    var kind = value.FixedArguments.Length > 0 && value.FixedArguments[0].Value is int k ? k : -1;
                    var loadType = value.FixedArguments.Length > 1 && value.FixedArguments[1].Value is int l ? l : -1;
                    string? name = null;
                    string? displayName = null;
                    foreach (var named in value.NamedArguments)
                    {
                        if (named.Name == "Name")
                        {
                            name = named.Value as string;
                        }
                        else if (named.Name == "DisplayName")
                        {
                            displayName = named.Value as string;
                        }
                    }

                    var reasons = new List<string>();
                    var hierarchy = new HashSet<string>(StringComparer.Ordinal);
                    var complete = scope.Hierarchy(metadata, handle, hierarchy, reasons);
                    marks.Add(new ModulePluginMark(file, FullName(metadata, type), (ModulePluginKind)kind, (PluginLoadType)loadType, name, displayName,
                        type.GetGenericParameters().Count != 0,
                        (type.Attributes & (TypeAttributes.Abstract | TypeAttributes.Interface)) != 0,
                        hierarchy.Contains(PluginInterface) ? true : complete ? false : (bool?)null,
                        hierarchy.Contains(DeviceSettingsInterface) ? true : complete ? false : (bool?)null,
                        reasons.Count == 0 ? null : string.Join("; ", reasons.Distinct())));
                }
            }

            return marks;
        }

        /// <summary>
        /// Checks the marks of a whole module (issue ModulePlugins-Instances, A, A.1, A.2).
        /// </summary>
        /// <param name="marks">every mark of the module</param>
        /// <param name="problems">receives what makes the module unusable</param>
        /// <param name="warnings">receives what could not be verified</param>
        public static void Check(IEnumerable<ModulePluginMark> marks, ICollection<string> problems, ICollection<string> warnings)
        {
            var infrastructureNames = new Dictionary<string, ModulePluginMark>(StringComparer.OrdinalIgnoreCase);
            foreach (var mark in marks)
            {
                var where = $"{mark.TypeName} ({mark.AssemblyFile})";
                if (!Enum.IsDefined(typeof(ModulePluginKind), mark.Kind))
                {
                    problems.Add($"{where}: the ModulePlugin mark has the unknown kind {(int)mark.Kind}.");
                }

                if (!Enum.IsDefined(typeof(PluginLoadType), mark.LoadType))
                {
                    problems.Add($"{where}: the ModulePlugin mark has the unknown load type {(int)mark.LoadType}.");
                }

                if (mark.IsOpenGeneric)
                {
                    problems.Add($"{where}: a ModulePlugin mark must be on a closed type; its type arguments would have to come from a type expression, which is code. Close the type in the module (class X : Base<Arg>).");
                }

                if (mark.IsAbstract)
                {
                    problems.Add($"{where}: a ModulePlugin mark must be on a class that can be built, not on an abstract type.");
                }

                if (mark.ImplementsPlugin == false)
                {
                    problems.Add($"{where}: carries the ModulePlugin mark but does not implement ITVComponents.Plugins.IPlugin.");
                }
                else if (mark.ImplementsPlugin == null)
                {
                    warnings.Add($"{where}: could not verify that it implements ITVComponents.Plugins.IPlugin: {mark.UnverifiedReason}");
                }

                if (mark.Kind == ModulePluginKind.Infrastructure)
                {
                    if (string.IsNullOrEmpty(mark.Name))
                    {
                        problems.Add($"{where}: an infrastructure plugin needs its fixed Name in the ModulePlugin mark.");
                    }
                    else if (!IsValidInstanceName(mark.Name))
                    {
                        problems.Add($"{where}: the name '{mark.Name}' is not valid; it must start with a letter and contain only letters, digits, '_', '.' or '-' (64 characters at most).");
                    }
                    else if (infrastructureNames.TryGetValue(mark.Name!, out var other))
                    {
                        problems.Add($"{where}: the infrastructure name '{mark.Name}' is also used by {other.TypeName} ({other.AssemblyFile}).");
                    }
                    else
                    {
                        infrastructureNames.Add(mark.Name!, mark);
                    }
                }
                else if (mark.Kind == ModulePluginKind.Template && mark.Name != null)
                {
                    problems.Add($"{where}: a template must not have a Name in the ModulePlugin mark; the name of each instance is given when it is created.");
                }
            }
        }

        private static string FullName(MetadataReader metadata, TypeDefinition type)
        {
            var declaring = type.GetDeclaringType();
            if (!declaring.IsNil)
            {
                return FullName(metadata, metadata.GetTypeDefinition(declaring)) + "+" + metadata.GetString(type.Name);
            }

            var ns = metadata.GetString(type.Namespace);
            return ns.Length == 0 ? metadata.GetString(type.Name) : ns + "." + metadata.GetString(type.Name);
        }

        private static bool IsAttribute(MetadataReader metadata, CustomAttribute attribute, string ns, string name)
        {
            switch (attribute.Constructor.Kind)
            {
                case HandleKind.MemberReference:
                    var member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    if (member.Parent.Kind != HandleKind.TypeReference)
                    {
                        return false;
                    }

                    var reference = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                    return metadata.StringComparer.Equals(reference.Name, name) && metadata.StringComparer.Equals(reference.Namespace, ns);
                case HandleKind.MethodDefinition:
                    var method = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                    var definition = metadata.GetTypeDefinition(method.GetDeclaringType());
                    return metadata.StringComparer.Equals(definition.Name, name) && metadata.StringComparer.Equals(definition.Namespace, ns);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Framework assemblies never implement IPlugin; they are not followed.
        /// </summary>
        private static bool IsFramework(string assemblyName)
            => assemblyName == "mscorlib" || assemblyName == "netstandard" || assemblyName == "System"
               || assemblyName.StartsWith("System.", StringComparison.Ordinal) || assemblyName.StartsWith("Microsoft.", StringComparison.Ordinal);

        /// <summary>
        /// The assemblies opened while following one type hierarchy; disposed together.
        /// </summary>
        private sealed class MetadataScope : IDisposable
        {
            private readonly Func<string, string?>? resolveAssembly;
            private readonly List<PEReader> open = new List<PEReader>();
            private readonly Dictionary<string, MetadataReader?> byName = new Dictionary<string, MetadataReader?>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, string> unavailable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<(MetadataReader, TypeDefinitionHandle)> visiting = new HashSet<(MetadataReader, TypeDefinitionHandle)>();

            public MetadataScope(Func<string, string?>? resolveAssembly)
            {
                this.resolveAssembly = resolveAssembly;
            }

            public MetadataReader? OpenRoot(string path)
            {
                var metadata = Open(path);
                if (metadata != null)
                {
                    byName[metadata.GetString(metadata.GetAssemblyDefinition().Name)] = metadata;
                }

                return metadata;
            }

            /// <summary>
            /// Collects the full names of the type, its base types and every interface it implements.
            /// </summary>
            /// <returns>whether the hierarchy could be followed completely (otherwise the reasons say where not)</returns>
            public bool Hierarchy(MetadataReader metadata, TypeDefinitionHandle handle, HashSet<string> names, List<string> reasons)
            {
                visiting.Clear();
                var before = reasons.Count;
                Definition(metadata, handle, 0, names, reasons);
                return reasons.Count == before;
            }

            public void Dispose()
            {
                foreach (var reader in open)
                {
                    reader.Dispose();
                }
            }

            private MetadataReader? Open(string path)
            {
                // The reader owns the stream; it is kept open only when the metadata is handed out.
                var reader = new PEReader(File.OpenRead(path));
                var keep = false;
                try
                {
                    if (!reader.HasMetadata)
                    {
                        return null;
                    }

                    var metadata = reader.GetMetadataReader();
                    if (!metadata.IsAssembly)
                    {
                        return null;
                    }

                    open.Add(reader);
                    keep = true;
                    return metadata;
                }
                finally
                {
                    if (!keep)
                    {
                        reader.Dispose();
                    }
                }
            }

            private MetadataReader? ByName(string assemblyName, List<string> reasons)
            {
                if (byName.TryGetValue(assemblyName, out var known))
                {
                    if (known == null)
                    {
                        reasons.Add(unavailable[assemblyName]);
                    }

                    return known;
                }

                MetadataReader? metadata = null;
                string? reason = null;
                var path = resolveAssembly?.Invoke(assemblyName);
                if (path == null || !File.Exists(path))
                {
                    reason = $"the assembly {assemblyName} was not found";
                }
                else
                {
                    try
                    {
                        metadata = Open(path);
                        if (metadata == null)
                        {
                            reason = $"{path} is no managed assembly";
                        }
                    }
                    catch (BadImageFormatException ex)
                    {
                        // Not swallowed: the reason travels into the warning of the check.
                        reason = $"{path} is not readable: {ex.Message}";
                    }
                }

                byName[assemblyName] = metadata;
                if (reason != null)
                {
                    unavailable[assemblyName] = reason;
                    reasons.Add(reason);
                }

                return metadata;
            }

            private void Any(MetadataReader metadata, EntityHandle handle, int depth, HashSet<string> names, List<string> reasons)
            {
                if (depth > MaxDepth)
                {
                    reasons.Add("the type hierarchy is too deep");
                    return;
                }

                switch (handle.Kind)
                {
                    case HandleKind.TypeDefinition:
                        Definition(metadata, (TypeDefinitionHandle)handle, depth, names, reasons);
                        return;
                    case HandleKind.TypeReference:
                        // The name counts even if its assembly is not available - IPlugin is found without the plugin
                        // abstractions at hand.
                        names.Add(ReferenceName(metadata, (TypeReferenceHandle)handle));
                        var resolved = Resolve(metadata, (TypeReferenceHandle)handle, reasons);
                        if (resolved != null)
                        {
                            Definition(resolved.Value.Metadata, resolved.Value.Handle, depth + 1, names, reasons);
                        }

                        return;
                    case HandleKind.TypeSpecification:
                        // A generic instantiation (Base<X>, IFoo<X>): its generic type counts.
                        var specification = metadata.GetTypeSpecification((TypeSpecificationHandle)handle);
                        var blob = metadata.GetBlobReader(specification.Signature);
                        if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance)
                        {
                            return;
                        }

                        blob.ReadSignatureTypeCode();
                        Any(metadata, blob.ReadTypeHandle(), depth + 1, names, reasons);
                        return;
                }
            }

            private static string ReferenceName(MetadataReader metadata, TypeReferenceHandle handle)
            {
                var reference = metadata.GetTypeReference(handle);
                var name = metadata.GetString(reference.Name);
                if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
                {
                    return ReferenceName(metadata, (TypeReferenceHandle)reference.ResolutionScope) + "+" + name;
                }

                var ns = metadata.GetString(reference.Namespace);
                return ns.Length == 0 ? name : ns + "." + name;
            }

            /// <summary>
            /// The definition of a referenced type; <c>null</c> for a framework type (nothing added to
            /// <paramref name="reasons"/>) or if it was not found (reason added).
            /// </summary>
            private (MetadataReader Metadata, TypeDefinitionHandle Handle)? Resolve(MetadataReader metadata, TypeReferenceHandle handle, List<string> reasons)
            {
                var reference = metadata.GetTypeReference(handle);
                var name = metadata.GetString(reference.Name);
                var ns = metadata.GetString(reference.Namespace);
                var scope = reference.ResolutionScope;
                switch (scope.Kind)
                {
                    case HandleKind.TypeReference:
                        // Not found or a framework type: the outer type already said which (reason or none).
                        var outer = Resolve(metadata, (TypeReferenceHandle)scope, reasons);
                        if (outer == null)
                        {
                            return null;
                        }

                        var outerMetadata = outer.Value.Metadata;
                        foreach (var nested in outerMetadata.GetTypeDefinition(outer.Value.Handle).GetNestedTypes())
                        {
                            if (outerMetadata.StringComparer.Equals(outerMetadata.GetTypeDefinition(nested).Name, name))
                            {
                                return (outerMetadata, nested);
                            }
                        }

                        reasons.Add($"the nested type {name} was not found");
                        return null;
                    case HandleKind.AssemblyReference:
                        var assemblyName = metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
                        if (IsFramework(assemblyName))
                        {
                            return null;
                        }

                        var target = ByName(assemblyName, reasons);
                        if (target == null)
                        {
                            return null;
                        }

                        var found = TopLevel(target, ns, name);
                        if (found == null)
                        {
                            reasons.Add($"the type {(ns.Length == 0 ? name : ns + "." + name)} was not found in {assemblyName} (forwarded?)");
                        }

                        return found == null ? ((MetadataReader, TypeDefinitionHandle)?)null : (target, found.Value);
                    case HandleKind.ModuleDefinition:
                    case HandleKind.ModuleReference:
                        var local = TopLevel(metadata, ns, name);
                        if (local == null)
                        {
                            reasons.Add($"the type {name} was not found in its own assembly");
                            return null;
                        }

                        return (metadata, local.Value);
                    default:
                        reasons.Add($"the type {name} has an unsupported resolution scope");
                        return null;
                }
            }

            private static TypeDefinitionHandle? TopLevel(MetadataReader metadata, string ns, string name)
            {
                foreach (var handle in metadata.TypeDefinitions)
                {
                    var type = metadata.GetTypeDefinition(handle);
                    if (type.GetDeclaringType().IsNil
                        && metadata.StringComparer.Equals(type.Name, name)
                        && metadata.StringComparer.Equals(type.Namespace, ns))
                    {
                        return handle;
                    }
                }

                return null;
            }

            private void Definition(MetadataReader metadata, TypeDefinitionHandle handle, int depth, HashSet<string> names, List<string> reasons)
            {
                if (!visiting.Add((metadata, handle)))
                {
                    return;
                }

                var type = metadata.GetTypeDefinition(handle);
                names.Add(FullName(metadata, type));
                foreach (var implementation in type.GetInterfaceImplementations())
                {
                    Any(metadata, metadata.GetInterfaceImplementation(implementation).Interface, depth + 1, names, reasons);
                }

                if (!type.BaseType.IsNil)
                {
                    Any(metadata, type.BaseType, depth + 1, names, reasons);
                }
            }
        }

        /// <summary>
        /// Decodes the two enum arguments (as int) and the string properties of the mark.
        /// </summary>
        private sealed class MarkDecoder : ICustomAttributeTypeProvider<object?>
        {
            public object? GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode;

            public object? GetSystemType() => typeof(Type);

            public object? GetSZArrayType(object? elementType) => null;

            public object? GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => "enum";

            public object? GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => "enum";

            public object? GetTypeFromSerializedName(string name) => "enum";

            // Both enums of the mark (ModulePluginKind, PluginLoadType) are int.
            public PrimitiveTypeCode GetUnderlyingEnumType(object? type) => PrimitiveTypeCode.Int32;

            public bool IsSystemType(object? type) => type is Type;
        }
    }
}
