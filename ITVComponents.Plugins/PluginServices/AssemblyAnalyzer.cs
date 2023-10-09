using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security;
using System.Text;
using System.Xml;
using ITVComponents.AssemblyResolving;
using ITVComponents.Logging;
using ITVComponents.Plugins.SelfRegistration;

namespace ITVComponents.Plugins.PluginServices
{
    public class AssemblyAnalyzer
    {
        //private static Type obsoleteCallbackType = GetObsoleteCallbackType();

        /// <summary>
        /// Describes all plugin-types in an assembly
        /// </summary>
        /// <param name="assemblyName">the assemblyname you want to describe</param>
        /// <returns>descriptors of all plugin types</returns>
        [SecurityCritical]
        public static TypeDescriptor[] DescribeAssembly(string assemblyName, Action<Type, TypeDescriptor> analyzeType = null, Action<ConstructorInfo, ConstructorDescriptor> analyzeConstructor = null, Action<ParameterInfo, ConstructorParameterDescriptor> analyzeParameter = null)
        {
            try
            {
                var location = AssemblyResolver.ResolveAssemblyLocation(assemblyName, out var exists);
                if (exists)
                {
                    using (AssemblyResolver.AcquireTemporaryLoadContext(out var isolatedContext))
                    {
                        Assembly analyzed = isolatedContext.LoadFromAssemblyPath(Path.GetFullPath(location)); //Assembly.LoadFrom(assemblyName);
                        return DescribeAssembly(analyzed);
                    }
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error, "PluginSystem");
            }

            return null;
        }

        /// <summary>
        /// Describes all plugin-types in an assembly
        /// </summary>
        /// <param name="assembly">the assembly you want to describe</param>
        /// <returns>descriptors of all plugin types</returns>
        [SecurityCritical]
        public static TypeDescriptor[] DescribeAssembly(Assembly assembly, Action<Type, TypeDescriptor> analyzeType = null, Action<ConstructorInfo, ConstructorDescriptor> analyzeConstructor = null, Action<ParameterInfo, ConstructorParameterDescriptor> analyzeParameter = null)
        {
            int objectId = 1;
            //var plug = typeof(IPlugin);
            var pluginTypes =
                (from t in assembly.GetTypes() where t.IsPublic && t.IsClass && !t.IsAbstract && t.GetConstructors().Any() select t);
            return (from t in pluginTypes select DescribeType(t, ref objectId, analyzeType, analyzeConstructor, analyzeParameter)).ToArray();
        }


        /// <summary>
        /// Describes a single type
        /// </summary>
        /// <param name="t">the type you want to describe</param>
        /// <returns>a type-descriptor containing all constructors of that type</returns>
        [SecurityCritical]
        public static TypeDescriptor DescribeType(Type t, Action<Type, TypeDescriptor> analyzeType = null,
            Action<ConstructorInfo, ConstructorDescriptor> analyzeConstructor = null,
            Action<ParameterInfo, ConstructorParameterDescriptor> analyzeParameter = null)
        {
            int objectId = 1;
            return DescribeType(t, ref objectId, analyzeType, analyzeConstructor, analyzeParameter);
        }

        /// <summary>
        /// Describes a single type
        /// </summary>
        /// <param name="t">the type you want to describe</param>
        /// <param name="objectId">the unique object id of this description run</param>
        /// <returns>a type-descriptor containing all constructors of that type</returns>
        private static TypeDescriptor DescribeType(Type t, ref int objectId, Action<Type, TypeDescriptor> analyzeType, Action<ConstructorInfo, ConstructorDescriptor> analyzeConstructor, Action<ParameterInfo, ConstructorParameterDescriptor> analyzeParameter)
        {
            ConstructorInfo[] constructors = t.GetConstructors();
            List<ConstructorDescriptor> constructorDescriptors = new List<ConstructorDescriptor>();
            var genericArguments = t.IsGenericTypeDefinition ? t.GetGenericArguments() : Array.Empty<Type>();
            int i = 0;
            foreach (ConstructorInfo info in constructors)
            {
                var constructor = DescribeConstructor(info, i, ref objectId, analyzeParameter);
                analyzeConstructor?.Invoke(info, constructor);
                constructorDescriptors.Add(constructor);
                i++;
            }
            TypeDescriptor retVal = new TypeDescriptor
                                        {
                                            Uid = objectId++,
                                            TypeFullName = t.FullName,
                                            Constructors =constructorDescriptors.ToArray(),
                                            GenericParameters= (from g in genericArguments select DescribeGenericParameter(g)).ToArray()
                                        };
            analyzeType?.Invoke(t, retVal);
            return retVal;
        }

        private static GenericParameterDescriptor DescribeGenericParameter(Type type)
        {
            var retVal = new GenericParameterDescriptor { GenericParameterName = type.Name };
            var constraints = type.GetGenericParameterConstraints();
            var sbl = new StringBuilder(@"Flags:
");
            ListGenericParameterAttributes(type, sbl);
            if (constraints.Length != 0)
            {
                sbl.AppendLine("Type-Constraints:");
                foreach (var constraint in constraints)
                {
                    sbl.AppendLine($"- Inherits {constraint.FullName??constraint.AssemblyQualifiedName??constraint.Name}");
                }
            }

            retVal.Remarks = sbl.ToString(0, sbl.Length - Environment.NewLine.Length);
            return retVal;
        }

        private static void ListGenericParameterAttributes(Type t, StringBuilder target)
        {
            GenericParameterAttributes gpa = t.GenericParameterAttributes;
            GenericParameterAttributes variance = gpa &
                                                  GenericParameterAttributes.VarianceMask;

            // Select the variance flags.
            if (variance == GenericParameterAttributes.None)
            {
                target.AppendLine("- No variance flag");
            }
            else
            {
                if ((variance & GenericParameterAttributes.Covariant) != 0)
                    target.AppendLine("- Covariant");
                else
                    target.AppendLine("- Contravariant");
            }

            // Select 
            GenericParameterAttributes constraints = gpa &
                                                     GenericParameterAttributes.SpecialConstraintMask;

            if (constraints == GenericParameterAttributes.None)
            {
                target.AppendLine("- No special constraints");
            }
            else
            {
                if ((constraints & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                    target.AppendLine("- ReferenceTypeConstraint (class)");
                if ((constraints & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                    target.AppendLine("- NotNullableValueTypeConstraint (struct)");
                if ((constraints & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                    target.AppendLine("- DefaultConstructorConstraint (new())");
            }
        }

        /// <summary>
        /// Describes the given Constructor
        /// </summary>
        /// <param name="c">the constructor info for which to get the information</param>
        /// <param name="index">the index of this constructor item</param>
        /// <param name="objectId">the unique id - provider for all object descriptors</param>
        /// <returns>a constructor descriptor for the given constructor info</returns>
        private static ConstructorDescriptor DescribeConstructor(ConstructorInfo c, int index, ref int objectId, Action<ParameterInfo, ConstructorParameterDescriptor> analyzeParameter)
        {
            ParameterInfo[] parameters = c.GetParameters().ToArray();
            List<ConstructorParameterDescriptor> parameterDescriptors = new List<ConstructorParameterDescriptor>();
            foreach (ParameterInfo param in parameters)
            {
                var arg = DescribeParameter(param, ref objectId);
                parameterDescriptors.Add(arg);
                analyzeParameter?.Invoke(param, arg);
            }

            ConstructorDescriptor retVal = new ConstructorDescriptor
                                               {
                                                   Uid = objectId++,
                                                   ConstructorName = string.Format(".ctor{0}",index+1),
                                                   Parameters =parameterDescriptors.ToArray(),
                                                   Sample = GenerateSample(c, parameterDescriptors)
                                               };

            return retVal;
        }

        /// <summary>
        /// Creates a sample string for the given constructor
        /// </summary>
        /// <param name="constructor">the constructor info for which to create a sample</param>
        /// <param name="parameters">the sample arguments for the given constructor</param>
        /// <returns>a sample string showing, how this constructor is defined</returns>
        private static string GenerateSample(ConstructorInfo constructor, IList<ConstructorParameterDescriptor> parameters)
        {
            string segment1 = Path.GetFileName(constructor.DeclaringType.Assembly.Location);
            string segment2 = constructor.DeclaringType.FullName;
            string segment3 = string.Join(",", from t in parameters select t.SampleValue);
            string retVal = $"[{segment1}]<{segment2}>{segment3}";
            return retVal;
        }

        /// <summary>
        /// Describes a Parameter
        /// </summary>
        /// <param name="t">the Target Parameter you want to describe</param>
        /// <param name="objectId">the unique id provider variable for this description run</param>
        /// <returns>a complete Parameter Description of the given parameter</returns>
        private static ConstructorParameterDescriptor DescribeParameter(ParameterInfo t, ref int objectId)
        {
            XmlElement element = DocsByReflection.DocsService.GetXmlFromParameter(t, false);
            return new ConstructorParameterDescriptor
                       {
                           Uid = objectId++,
                           ParameterName = t.Name,
                           ParameterType = t.ParameterType.FullName??t.ParameterType.AssemblyQualifiedName??t.ParameterType.Name,
                           TypePrefix = GetPrefix(t.ParameterType),
                           SampleValue = GetSampleValue(t.ParameterType),
                           ParameterDescription = element != null ? element.ToString() : ""
                       };
        }

        /// <summary>
        /// Gets the prefix for the given parameter type
        /// </summary>
        /// <param name="parameterType">the type of a constructor parameter</param>
        /// <returns>the required prefix inside a constructor string</returns>
        private static string GetPrefix(Type parameterType)
        {
            string retVal = "$";
            if (parameterType == typeof(long))
            {
                retVal = "L";
            }
            else if (parameterType == typeof(int))
            {
                retVal = "I";
            }
            else if (parameterType == typeof(double))
            {
                retVal = "D";
            }
            else if (parameterType == typeof(float))
            {
                retVal = "F";
            }
            else if (parameterType == typeof(bool))
            {
                retVal = "B";
            }
            else if (parameterType == typeof(string))
            {
                retVal = "[$]\"";
            }
            else if (parameterType.IsEnum || parameterType.IsPrimitive)
            {
                retVal = "\"^^";
            }
            return retVal;
        }

        /// <summary>
        /// Gets the sample value for the given type
        /// </summary>
        /// <param name="parameterType">the type of a constructor parameter</param>
        /// <returns>a sample value for the given type</returns>
        private static string GetSampleValue(Type parameterType)
        {
            string retVal = "$SomePlugin";
            if (parameterType == typeof(long))
            {
                retVal = "L0";
            }
            else if (parameterType == typeof(int))
            {
                retVal = "I0";
            }
            else if (parameterType == typeof(double))
            {
                retVal = "D0.0";
            }
            else if (parameterType == typeof(float))
            {
                retVal = "F0.0";
            }
            else if (parameterType == typeof(bool))
            {
                retVal = "BTrue";
            }
            else if (parameterType == typeof(string))
            {
                retVal = "[$]\"SomeString\"";
            }
            else if (parameterType.IsEnum || parameterType.IsPrimitive)
            {
                if (parameterType.IsEnum)
                {
                    retVal = $"\"^^'{parameterType.FullName}@@\\\"{parameterType.Assembly.FullName}\\\"'.{Enum.GetNames(parameterType).First()}\"";
                }
                else
                {
                    retVal = "\"^^SomeExpression\"";
                }
            }
            else if (parameterType.GetInterfaces().Contains(typeof(IPluginFactory)))
            {
                retVal = "$factory";
            }

            return retVal;
        }

        /*[Obsolete]
        private static Type GetObsoleteCallbackType()
        {
            return typeof(SelfRegistrationCallback);
        }*/

        /*private static Type AsReflectOnlyType(Type runtimeType)
        {
            var ass = Assembly.ReflectionOnlyLoadFrom(runtimeType.Assembly.Location);
            var retTyp = ass.GetType(runtimeType.FullName);
            return retTyp;
        }*/
    }
}