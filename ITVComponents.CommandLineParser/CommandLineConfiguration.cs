using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.TypeConversion;
namespace ITVComponents.CommandLineParser
{
    internal class CommandLineConfiguration
    {
        /// <summary>
        /// A list of supported parameters
        /// </summary>
        private CommandLineArgument[] parameters;

        /// <summary>
        /// Indicates whether the parameters are case sensitive
        /// </summary>
        private bool caseSensitive;

        /// <summary>
        /// Initializes a new instance of the CommandLineConfiguration class
        /// </summary>
        /// <param name="parameters">the parameters you want to support</param>
        /// <param name="caseSensitive">indicates whether the parameters must be compared with case</param>
        public CommandLineConfiguration(CommandLineArgument[] parameters, bool caseSensitive)
        {
            this.parameters = parameters;
            this.caseSensitive = caseSensitive;
        }

        /// <summary>
        /// Gets the parameters attached to this Program
        /// </summary>
        internal CommandLineArgument[] Parameters { get { return parameters; } }

        /// <summary>
        /// Checks the next commandline argument and returns a value indicating whether the value is known and valid
        /// </summary>
        /// <param name="command">the complete command line</param>
        /// <param name="position">the current position</param>
        /// <param name="argument">the argument that was found</param>
        /// <param name="value">the value of the argument</param>
        /// <returns>a value indicating whether the provided parameter is known and valid</returns>
        public bool CheckArgument(string command, ref int position, out CommandLineArgument argument, out object value)
        {
            int nextPos = position;
            bool completlyQuoted = false;
            while (nextPos < command.Length && char.IsWhiteSpace(command, nextPos))
            {
                nextPos++;
            }

            int startPos = nextPos;
            completlyQuoted = command.Substring(startPos, 1) == "\"";

            nextPos = 1;
            CommandLineArgument[] args;
            while ((args = (from t in parameters
                            where
                                t.ArgumentName.StartsWith(command.Substring(!completlyQuoted?startPos:startPos+1, nextPos),
                                                          caseSensitive
                                                              ? StringComparison.Ordinal
                                                              : StringComparison.OrdinalIgnoreCase)
                            select t).ToArray()).Length > 1)
            {
                nextPos++;
            }

            if (args.Length == 0)
            {
                args = (from t in parameters
                        where t.ArgumentName.Equals("tail", caseSensitive
                                                                ? StringComparison.Ordinal
                                                                : StringComparison.OrdinalIgnoreCase)
                        select t).ToArray();
            }

            if (args.Length == 0)
            {
                value = null;
                argument = null;
                return false;
            }

            argument = args[0];
            if (!argument.ArgumentName.Equals("tail", caseSensitive
                                                          ? StringComparison.Ordinal
                                                          : StringComparison.OrdinalIgnoreCase))
            {
                int spo = !completlyQuoted ? startPos : startPos + 1;
                bool isOk = spo + argument.ArgumentName.Length <= command.Length;
                bool isEnd = (spo + argument.ArgumentName.Length == command.Length || argument.TargetProperty.PropertyType != typeof(bool));
                if (isOk)
                {
                    string cmd =
                        command.Substring(!completlyQuoted ? startPos : startPos + 1,
                                          argument.ArgumentName.Length + (isEnd ? 0 : 1)).Trim();
                    if (cmd.Equals(argument.ArgumentName, caseSensitive
                                                              ? StringComparison.Ordinal
                                                              : StringComparison.OrdinalIgnoreCase))
                    {
                        startPos = startPos + argument.ArgumentName.Length + (completlyQuoted ? 1 : 0);
                    }
                    else
                    {
                        value = null;
                        argument = null;
                        return false;
                    }
                }
                else
                {
                    value = null;
                    argument = null;
                    return false;
                }
            }

            value = true;
            nextPos = 0;
            if (argument.TargetProperty.PropertyType != typeof(bool))
            {
                if (!argument.ArgumentName.Equals("tail", caseSensitive
                                                              ? StringComparison.Ordinal
                                                              : StringComparison.OrdinalIgnoreCase))
                {
                    while (char.IsWhiteSpace(command, startPos))
                    {
                        startPos++;
                    }
                }

                nextPos = 1;
                bool isQuoted = command.Substring(startPos, 1) == "\"" | completlyQuoted;
                while (startPos + nextPos < command.Length &&
                       ((!isQuoted && !char.IsWhiteSpace(command, startPos + nextPos)) ||
                        (isQuoted &&
                         (command.Substring(startPos + nextPos, 1) != "\"" ||
                          (command.Length > startPos + nextPos + 1 && command.Substring(startPos + nextPos, 2) == "\"\"")))))
                {
                    if (isQuoted && command.Substring(startPos + nextPos, 2) == "\"\"")
                    {
                        nextPos++;
                    }

                    nextPos++;
                }

                string rawVal = command.Substring(startPos, nextPos);
                if (isQuoted)
                {
                    rawVal = rawVal.Substring(completlyQuoted ? 0 : 1, rawVal.Length - (completlyQuoted ? 0 : 1)).Replace("\"\"","\"");
                }

                try
                {
                    value = ChangeType(rawVal, argument.TargetProperty.PropertyType);
                }
                catch (Exception ex)
                {
                    throw new CommandLineSyntaxException(argument.ArgumentName);
                }
            }

            position = startPos + nextPos+1;
            return true;
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
            StringBuilder bld = new StringBuilder();
            int maxParameterLength = (from t in parameters select t.ArgumentName.Length).Max();
            string indentString = new string(' ',indent);
            string parameterFormatString = string.Format("{{0}}{{1,-{0}}}   {{2}}\r\n", maxParameterLength);
            bld.AppendLine("usage:");
            string app = includeProgram
                             ? string.Format("{0} ", Path.GetFileName(Assembly.GetEntryAssembly().Location))
                             : "";
            string appLine = string.Join(" ", (from t in parameters
                                               select
                                                   (!t.IsOptional
                                                        ? (!t.ArgumentName.Equals("tail", caseSensitive
                                                              ? StringComparison.Ordinal
                                                              : StringComparison.OrdinalIgnoreCase) ? t.ArgumentName : "")
                                                        : string.Format("[{0}", (!t.ArgumentName.Equals("tail", caseSensitive
                                                              ? StringComparison.Ordinal
                                                              : StringComparison.OrdinalIgnoreCase) ? t.ArgumentName : ""))) + " " +
                                                   (t.TargetProperty.PropertyType != typeof(bool) ? t.TargetProperty.Name : "") +
                                                   (t.IsOptional ? "]" : "")));
            bld.Append(app);
            bld.AppendLine(FormatDescription(maxCharactersPerLine, app.Length, appLine));
            bld.AppendLine();
            foreach (CommandLineArgument argument in parameters)
            {
                bld.AppendFormat(parameterFormatString, indentString, (!argument.ArgumentName.Equals("tail", caseSensitive
                                                              ? StringComparison.Ordinal
                                                              : StringComparison.OrdinalIgnoreCase) ? argument.ArgumentName : argument.TargetProperty.Name),
                                 FormatDescription(maxCharactersPerLine, indent + maxParameterLength+3,
                                                   argument.ParameterDescription));
            }

            return bld.ToString();
        }

        /// <summary>
        /// Formats the description of the current parameter
        /// </summary>
        /// <param name="maxCharactersPerLine">the maximum length of a line</param>
        /// <param name="indent">the number of indent spaces</param>
        /// <param name="description">the description of the parameter</param>
        /// <returns>a formatted description of the given parameter</returns>
        private string FormatDescription(int maxCharactersPerLine, int indent, string description)
        {
            if (description.Length > maxCharactersPerLine - indent)
            {
                int ln = 0;
                int start = 0;
                int maxLen = maxCharactersPerLine - indent;
                string indentChar = new string(' ', indent);
                StringBuilder retVal = new StringBuilder();
                int next = description.IndexOf(' ', start);
                while (next != -1)
                {
                    if (ln + (next - start) + 1 > maxLen)
                    {
                        retVal.AppendLine();
                        retVal.Append(indentChar);
                        ln = 0;
                    }

                    retVal.AppendFormat("{0} ", description.Substring(start, next - start));
                    ln += (next - start + 1);
                    start = next + 1;
                    next = description.IndexOf(' ', start);
                }

                if (ln + (description.Length - start) > maxLen)
                {
                    retVal.AppendLine();
                    retVal.Append(indentChar);
                }

                retVal.Append(description.Substring(start));
                return retVal.ToString();
            }

            return description;
        }

        /// <summary>
        /// Changes the Type of an expression to an other. Works only with IConvertible implementations and enums.
        /// </summary>
        /// <param name="value">the value that was provided to the commandline</param>
        /// <param name="targetType">the target type to which the value must be converted</param>
        /// <returns>the conversion result.</returns>
        private object ChangeType(string value, Type targetType)
        {
            if (targetType == typeof (string))
            {
                return value;
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value);
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return ChangeType(value, Nullable.GetUnderlyingType(targetType));

            }

            return TypeConverter.Convert(value, targetType);
        }
    }
}
