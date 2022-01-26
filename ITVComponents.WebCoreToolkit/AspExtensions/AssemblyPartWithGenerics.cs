using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace ITVComponents.WebCoreToolkit.AspExtensions
{
    public class AssemblyPartWithGenerics:ApplicationPart, IApplicationPartTypeProvider
    {
        private readonly Dictionary<string, Type> typeArguments;

        /// <summary>
        /// Initializes a new <see cref="AssemblyPart"/> instance.
        /// </summary>
        /// <param name="assembly">The backing <see cref="System.Reflection.Assembly"/>.</param>
        public AssemblyPartWithGenerics(Assembly assembly, Dictionary<string, Type> typeArguments)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            this.typeArguments = typeArguments ?? throw new ArgumentException(nameof(typeArguments));
        }

        /// <summary>
        /// Gets the <see cref="Assembly"/> of the <see cref="ApplicationPart"/>.
        /// </summary>
        public Assembly Assembly { get; }

        /// <summary>
        /// Gets the name of the <see cref="ApplicationPart"/>.
        /// </summary>
        public override string Name => Assembly.GetName().Name!;

        /// <summary>
        /// Gets the list of available types in the <see cref="T:Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPart" />.
        /// </summary>
        public IEnumerable<TypeInfo> Types => BuildTypes();

        private IEnumerable<TypeInfo> BuildTypes()
        {
            var assemblyTypes = Assembly.DefinedTypes.ToArray();
            foreach (var type in assemblyTypes)
            {
                if (!type.IsGenericTypeDefinition)
                {
                    yield return type;
                }
                else
                {
                    var t = BuildGenericType(type);
                    if (t != null)
                    {
                        yield return t;
                    }
                }
            }
        }

        private TypeInfo BuildGenericType(TypeInfo type)
        {
            var arg = type.GenericTypeParameters;
            var nt = new Type[arg.Length];
            var success = true;
            for (var index = 0; index < arg.Length; index++)
            {
                var t = arg[index];
                if (typeArguments.ContainsKey(t.Name))
                {
                    nt[index] = typeArguments[t.Name];
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
                    var gn = type.MakeGenericType(nt);
                    return gn.GetTypeInfo();
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
