using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ITVComponents.CommandLineParser
{
    public class CommandLineParser
    {
        /// <summary>
        /// the configuration that was provided for
        /// </summary>
        private CommandLineConfiguration configuration;

        /// <summary>
        /// the target type that will be configured with the passed commandline arguments
        /// </summary>
        private Type targetType;

        /// <summary>
        /// indicates whether to compare commandline parameters case sensitive
        /// </summary>
        private bool caseSensitive;

        /// <summary>
        /// Initializes a new instance of the CommandLineParser class
        /// </summary>
        /// <param name="targetType">the target target type that is configured with the evaluated parameters</param>
        /// <param name="caseSensitive">indicates whether to compare parameters using their case. Defaultvalue: true</param>
        public CommandLineParser(Type targetType, bool caseSensitive=true)
        {
            this.caseSensitive = caseSensitive;
            this.targetType = targetType;
            configuration = BuildConfiguration();
        }

        /// <summary>
        /// Runs the target method with the commandline provided parameters
        /// </summary>
        /// <param name="commandLine">the commandline that was provided by the caller of the current application</param>
        public void Configure(string commandLine, object targetObject)
        {
            CommandLineArgument[] parameters = configuration.Parameters;
            Dictionary<PropertyInfo, object> parameterValues = new Dictionary<PropertyInfo, object>();
            Dictionary<PropertyInfo, bool> parametersUsed = new Dictionary<PropertyInfo, bool>();
            bool helpRequested = false;
            foreach (CommandLineArgument arg in parameters)
            {
                PropertyInfo param = arg.TargetProperty;
                parameterValues.Add(param, null);
                parametersUsed.Add(param, false);
            }

            int pos = 0;
            while (pos < commandLine.Length && pos != -1)
            {
                CommandLineArgument arg;
                object val;
                if (!configuration.CheckArgument(commandLine, ref pos, out arg, out val))
                {

                    throw new CommandLineSyntaxException();
                }

                PropertyInfo param = arg.TargetProperty;
                parameterValues[param] = val;
                parametersUsed[param] = true;
                if (arg.IsHelpParameter)
                {
                    helpRequested = true;
                }
            }

            foreach (var tmp in (from u in configuration.Parameters
                                 join v in parametersUsed on u.TargetProperty equals v.Key
                                 where !v.Value
                                 select u))
            {
                if ((!tmp.IsOptional && tmp.TargetProperty.PropertyType != typeof (bool)) && !helpRequested)
                {
                    throw new CommandLineSyntaxException(tmp.ArgumentName);
                }
                else if (tmp.TargetProperty.PropertyType == typeof (bool))
                {
                    parameterValues[tmp.TargetProperty] = false;
                }
            }

            foreach (CommandLineArgument argument in parameters)
            {
                object val;
                if ((val = parameterValues[argument.TargetProperty]) != null)
                {
                    argument.TargetProperty.SetValue(targetObject, val, null);
                }
            }
        }

        /// <summary>
        /// Runs the target method with the commandline provided parameters
        /// </summary>
        /// <param name="commandLine">the commandline that was provided by the caller of the current application</param>
        /// <param name="targetObject">the object on which to build the configuration provided by the caller</param>
        public void Configure(string[] commandLine, object targetObject)
        {
            Configure(string.Join(" ", (from t in commandLine select t.Contains("\"")?string.Format("\"{0}\"",t.Replace("\"","\"\"")):t into b
            select b.Contains(" ")&&!b.StartsWith("\"") ? string.Format("\"{0}\"", b) : b)),
                targetObject);
        }

        /// <summary>
        /// Builds the configuration for the provided class
        /// </summary>
        /// <returns>a configuration collection used to set configuration values on the target object</returns>
        private CommandLineConfiguration BuildConfiguration()
        {
            PropertyInfo[] properties =
                targetType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public |
                                         BindingFlags.SetProperty | BindingFlags.InvokeMethod);
            List<CommandLineArgument> arguments = new List<CommandLineArgument>();
            foreach (PropertyInfo property in properties)
            {
                CommandParameterAttribute attribute = (CommandParameterAttribute)Attribute.GetCustomAttribute(property,
                                                                                   typeof (CommandParameterAttribute),
                                                                                   true);
                if (attribute != null)
                {
                    arguments.Add(new CommandLineArgument
                                      {
                                          ArgumentName = attribute.ArgumentName,
                                          IsHelpParameter = attribute.IsHelpParameter,
                                          IsOptional = attribute.IsOptional,
                                          ParameterDescription = attribute.ParameterDescription,
                                          TargetProperty = property
                                      });
                }
            }

            return new CommandLineConfiguration(arguments.ToArray(), caseSensitive);
        }

        /// <summary>
        /// Creates a string that illustrates the usage of the commandline-parsable product
        /// </summary>
        /// <param name="includeProgram">indicates whether to include the name of the entry-assembly</param>
        /// <param name="indent">the indent that is used for the parameters</param>
        /// <param name="maxCharactersPerLine">the maximum length of a line</param>
        /// <returns>a complete usage description of the module that is configured with this parser</returns>
        public string PrintUsage(bool includeProgram, int indent, int maxCharactersPerLine)
        {
            return configuration.PrintUsage(includeProgram, indent, maxCharactersPerLine);
        }
    }
}
