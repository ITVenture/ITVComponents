using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Initialization;
using Exception = System.Exception;

namespace ITVComponents.Plugins.PluginServices
{
    public class PluginConstructorParser
    {
        /// <summary>
        /// Regex used to recognize strings including escape sequences
        /// </summary>
        private static readonly Regex StringRecognizer =
            new Regex(
                @"(?<formatIndicator>\$?)\""(?<string>([^\\\""]|\\\""|\\\\|\\a|\\b|\\f|\\n|\\r|\\t|\\u\:[0-9a-f]{4}|\\x[0-9a-f]{1,4}|\\[0-7]{3})*)\""",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase |
                RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

        /// <summary>
        /// Regex used to recognize escape sequences in strings
        /// </summary>
        private static readonly Regex EscapeRecognizer = new Regex(@"\\(?<ident>u:|x|a|r|n|t|f|\\|\"")(?<args>[0-9a-f]*)");

        /// <summary>
        /// Parses constructorstrings for further processing
        /// </summary>
        private static readonly Regex ConstructorParser = new Regex(@"\[(?<Path>[^\]]+)\]\<(?<Type>[\w \. ` _ \+]*)\>(?<Parameters>.*)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses a constructor hint for a Plugin
        /// </summary>
        /// <param name="constructorString">the constructor hint</param>
        /// <param name="formatProvider">a Plugin-instance that is capable for formatting custom strings (i.e. decrypting passwords, or buffering sql-server instance names)</param>
        public static PluginConstructionElement ParsePluginString(string constructorString, Dictionary<string,object> customStringFormatArguments, IStringFormatProvider formatProvider)
        {
            try
            {
                Match splitted = ConstructorParser.Match(constructorString);
                string filename = splitted.Groups["Path"].Value;
                string className = splitted.Groups["Type"].Value;
                PluginParameterElement[] constructor =
                    ParseConstructor(splitted.Groups["Parameters"].Value, customStringFormatArguments, formatProvider);
                return new PluginConstructionElement
                {
                    AssemblyName = filename,
                    TypeName = className,
                    Parameters = constructor
                };
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error);
                LogEnvironment.LogEvent(constructorString, LogSeverity.Error);
                throw;
            }
        }

        /// <summary>
        /// parses the constructorparameter string for a LogAdapter
        /// </summary>
        /// <param name="constructor">the constructorparameter string</param>
        /// <param name="formatProvider">a Plugin-instance that is capable for formatting custom strings (i.e. decrypting passwords, or buffering sql-server instance names)</param>
        /// <returns>an object array containing the parsed objects</returns>
        private static PluginParameterElement[] ParseConstructor(string constructor, Dictionary<string,object> customStringFormatArguments, IStringFormatProvider formatProvider)
        {
            List<PluginParameterElement> ls = new List<PluginParameterElement>();
            List<string> strings = new List<string>();
            if (constructor != string.Empty)
            {
                constructor = StringRecognizer.Replace(constructor, (m) =>
                {
                    int id = strings.Count;
                    var tmp = Unescape(m.Groups["string"].Value);
                    if (m.Groups["formatIndicator"].Value == "$" && formatProvider != null)
                    {
                        LogEnvironment.LogDebugEvent($"Resolving {tmp} using {formatProvider}...",LogSeverity.Report);
                        tmp = formatProvider.ProcessLiteral(tmp, customStringFormatArguments);
                        LogEnvironment.LogDebugEvent($"Resulted to {tmp}",LogSeverity.Report);
                    }

                    strings.Add(tmp);
                    return string.Format("#STRING##{0}##", id);
                });
                string[] args = constructor.Split(new[] { "," }, StringSplitOptions.None);
                foreach (string arg in args)
                {
                    if (!arg.StartsWith("#"))
                    {
                        AddConstructorVal(arg.ToUpper()[0], arg.Substring(1), ls);
                    }
                    else
                    {
                        string tmpValue = strings[int.Parse(arg.Substring(9, arg.Length - 11))];
                        AddConstructorVal((!tmpValue.StartsWith("^^")) ? 'S' : 'O', tmpValue, ls);
                    }
                }
            }

            return ls.ToArray();
        }

        /// <summary>
        /// Adds a Constructor parameter to a list
        /// </summary>
        /// <param name="type">the Type of the Parameter</param>
        /// <param name="value">the Value to put into the list</param>
        /// <param name="list">the Target list where the parsed Parameter must be stored</param>
        private static void AddConstructorVal(char type, string value, List<PluginParameterElement> list)
        {
            PluginParameterElement retVal = new PluginParameterElement();
            switch (type)
            {
                case 'S':
                    {
                        retVal.LiteralKind = LiteralKind.String;
                        retVal.TypeOfParameter = ParameterKind.Literal;
                        retVal.ParameterValue = value;
                        break;
                    }

                case 'L':
                    {
                        retVal.LiteralKind = LiteralKind.Long;
                        retVal.TypeOfParameter = ParameterKind.Literal;
                        retVal.ParameterValue = long.Parse(value);
                        break;
                    }

                case 'I':
                    {
                        retVal.LiteralKind = LiteralKind.Int;
                        retVal.TypeOfParameter = ParameterKind.Literal;
                        retVal.ParameterValue = int.Parse(value);
                        break;
                    }

                case 'D':
                    {
                        retVal.LiteralKind = LiteralKind.Double;
                        retVal.TypeOfParameter = ParameterKind.Literal;
                        retVal.ParameterValue = double.Parse(value);
                        break;
                    }

                case 'F':
                    {
                        retVal.LiteralKind = LiteralKind.Single;
                        retVal.TypeOfParameter = ParameterKind.Literal;
                        retVal.ParameterValue = float.Parse(value);
                        break;
                    }
                case 'B':
                    {
                        retVal.LiteralKind = LiteralKind.Boolean;
                        retVal.TypeOfParameter = ParameterKind.Literal;
                        retVal.ParameterValue = bool.Parse(value);
                        break;
                    }
                case '$':
                    {
                        retVal.TypeOfParameter = ParameterKind.Plugin;
                        retVal.ParameterValue = value;
                        break;
                    }

                case 'O':
                    {
                        retVal.TypeOfParameter = ParameterKind.Expression;
                        retVal.ParameterValue = value.Substring(2);
                        break;
                    }
            }

            list.Add(retVal);
        }

        /// <summary>
        /// Un-Escapes a string
        /// </summary>
        /// <param name="str">a string that was provided inside a constructor hint</param>
        /// <returns>the un-escaped representation of the provided string</returns>
        private static string Unescape(string str)
        {
            return EscapeRecognizer.Replace(str, (m) =>
            {
                switch (m.Groups["ident"].Value)
                {
                    case "a":
                    case "A":
                        {
                            return "\a";
                        }
                    case "b":
                    case "B":
                        {
                            return "\b";
                        }
                    case "f":
                    case "F":
                        {
                            return "\f";
                        }
                    case "n":
                    case "N":
                        {
                            return "\n";
                        }
                    case "r":
                    case "R":
                        {
                            return "\r";
                        }
                    case "t":
                    case "T":
                        {
                            return "\t";
                        }
                    case "u:":
                    case "U:":
                    case "x":
                    case "X":
                        {
                            return
                                ((char)
                                int.Parse(m.Groups["args"].Value,
                                          NumberStyles.HexNumber)).ToString();
                        }
                    case "\"":
                    case "\\":
                        {
                            return $"{m.Groups["ident"].Value}{m.Groups["args"].Value ?? ""}";
                        }
                    default:
                        {
                            return
                                ((char)
                                 Convert.ToInt32(m.Groups["args"].Value, 8))
                                    .ToString();
                        }
                }
            });
        }
    }
}
