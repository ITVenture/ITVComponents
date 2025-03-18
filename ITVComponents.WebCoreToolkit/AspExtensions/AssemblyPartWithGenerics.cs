using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Razor.Hosting;

namespace ITVComponents.WebCoreToolkit.AspExtensions
{
    public class AssemblyPartWithGenerics:ApplicationPart, IApplicationPartTypeProvider
    {
        private readonly Dictionary<string, Type> typeArguments;
        private readonly Dictionary<string, Type> customTypeArguments;
        private readonly TypeRegisterBehavior defaultBehavior;
        private readonly CustomTypeRegisterBehavior[] customBehaviors;
        private TypeInfo[] finalTypes;

        /// <summary>
        /// Initializes a new <see cref="AssemblyPart"/> instance.
        /// </summary>
        /// <param name="assembly">The backing <see cref="System.Reflection.Assembly"/>.</param>
        public AssemblyPartWithGenerics(Assembly assembly, Dictionary<string, Type> typeArguments, Dictionary<string, Type> customTypeArguments = null,
            TypeRegisterBehavior defaultBehavior = TypeRegisterBehavior.Use, CustomTypeRegisterBehavior[] customBehaviors = null)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            this.typeArguments = typeArguments ?? throw new ArgumentException(nameof(typeArguments));
            this.customTypeArguments = customTypeArguments;
            this.defaultBehavior = defaultBehavior;
            this.customBehaviors = customBehaviors;
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
        public IEnumerable<TypeInfo> Types
        {
            get
            {
                if (finalTypes == null)
                {
                    BuildTypes();
                }

                return finalTypes;
            }
        }

        private void BuildTypes()
        {
            List<TypeInfo> resultingTypes = new List<TypeInfo>();
            var assemblyTypes = Assembly.DefinedTypes.ToArray();
            foreach (var type in assemblyTypes)
            {
                var loadBehavior = defaultBehavior;
                var customBehavior = customBehaviors?.FirstOrDefault(n => n.Type == type);
                if (customBehavior != null && customBehavior.LoadBehavior != TypeRegisterBehavior.Default)
                {
                    loadBehavior = customBehavior.LoadBehavior;
                }

                if (loadBehavior == TypeRegisterBehavior.Use)
                {
                    if (!type.IsGenericTypeDefinition)
                    {
                        resultingTypes.Add(type);
                    }
                    else
                    {
                        var t = BuildGenericType(type);
                        if (t != null)
                        {
                            loadBehavior = defaultBehavior;
                            customBehavior = customBehaviors?.FirstOrDefault(n => n.Type == type);
                            if (customBehavior != null && customBehavior.LoadBehavior != TypeRegisterBehavior.Default)
                            {
                                loadBehavior = customBehavior.LoadBehavior;
                            }

                            if (loadBehavior == TypeRegisterBehavior.Use)
                            {
                                resultingTypes.Add(t);
                            }
                        }
                    }
                }
            }

            finalTypes = resultingTypes.ToArray();
        }

        private TypeInfo BuildGenericType(TypeInfo type)
        {
            var arg = type.GenericTypeParameters;
            var nt = new Type[arg.Length];
            var success = true;
            var defaultArgs = Attribute.GetCustomAttributes(type).Where(n => n is CustomGenericTypeArgAttribute).Cast<CustomGenericTypeArgAttribute>().ToArray();
            for (var index = 0; index < arg.Length; index++)
            {
                var t = arg[index];
                if (typeArguments.ContainsKey(t.Name))
                {
                    nt[index] = typeArguments[t.Name];
                }
                else if (customTypeArguments != null && customTypeArguments.ContainsKey(t.Name))
                {
                    nt[index] = customTypeArguments[t.Name];
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
