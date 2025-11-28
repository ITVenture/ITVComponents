using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.DataSources;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Helpers
{
    public static class DiagnoseQueryHelper
    {
        public static DiagEntityAnlyseItem[] AnalyseViewModel<T>()
        {
            var retType = typeof(T);
            return (from t in retType.GetProperties(BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance) where Attribute.IsDefined(t, typeof(DiagnosticResultAttribute)) select new DiagEntityAnlyseItem{Attribute = (DiagnosticResultAttribute) Attribute.GetCustomAttribute(t, typeof(DiagnosticResultAttribute)), Property = t}).ToArray();
        }

        public static IDictionary<string, object> VerifyArguments(DiagnosticsQueryDefinition query, IDictionary<string, object> arguments, out bool argumentsValid)
        {
            var retVal = new Dictionary<string, object>();
            argumentsValid = VerifyArguments(query, arguments, (name, value, type, nullable) => retVal.Add(name, value), (name, type, nullable) => retVal.Add(name, DBNull.Value));
            return retVal;
        }
        
        public static IDictionary<string, object> BuildArguments(DiagnosticsQueryDefinition query, IDictionary<string, string> arguments, out bool argumentsValid)
        {
            var retVal = new Dictionary<string, object>();
            argumentsValid = BuildArguments(query, arguments, (name, value, type, nullable) => retVal.Add(name, value), (name, type, nullable) => retVal.Add(name, DBNull.Value));
            return retVal;
        }
        
        public static bool VerifyArguments(DiagnosticsQueryDefinition query, IDictionary<string, object> queryArguments, StringBuilder fullQuery)
        {
            var writeParam = new Action<string, string, bool>((name, type, nullable) =>
            {
                fullQuery.AppendLine($"{type}{(nullable ? "?" : "")} {name} = Global.{name};");
            });

            return VerifyArguments(query, queryArguments, (name, value, type, nullable) =>
            {
                if (!queryArguments.ContainsKey(name))
                {
                    queryArguments.Add(name, value);
                }

                writeParam(name, type, nullable);
            }, writeParam);
        }

        private static bool VerifyArguments(DiagnosticsQueryDefinition query, IDictionary<string, object> rawArguments, Action<string, object, string, bool> paramWithValue, Action<string, string, bool> paramWithoutValue)
        {
            return ProcessArguments(query, s =>
            {
                object retVal = null;
                if (rawArguments.ContainsKey(s))
                {
                    retVal = rawArguments[s];
                }

                return retVal;
            }, paramWithValue, paramWithoutValue);
        }

        public static bool BuildArguments(DiagnosticsQueryDefinition query, IDictionary<string, string> arguments, IDictionary<string, object> queryArguments, StringBuilder fullQuery)
        {
            var writeParam = new Action<string, string, bool>((name, type, nullable) =>
            {
                fullQuery.AppendLine($"{type}{(nullable ? "?" : "")} {name} = Global.{name};");
            });

            return BuildArguments(query, arguments, (name, value, type, nullable) =>
            {
                if (!queryArguments.ContainsKey(name))
                {
                    queryArguments.Add(name, value);
                }

                writeParam(name, type, nullable);
            }, writeParam);
        }

        private static bool BuildArguments(DiagnosticsQueryDefinition query, IDictionary<string, string> arguments, Action<string, object, string, bool> paramWithValue, Action<string, string, bool> paramWithoutValue)
        {
            return ProcessArguments(query, s =>
            {
                object retVal = null;
                if (arguments.ContainsKey(s))
                {
                    retVal = arguments[s];
                }

                return retVal;
            }, paramWithValue, paramWithoutValue);
        }

        private static bool ProcessArguments(DiagnosticsQueryDefinition query, Func<string, object> argumentValue, Action<string, object, string, bool> paramWithValue, Action<string, string, bool> paramWithoutValue)
        {
            bool retVal = true;
            foreach (var arg in query.Parameters)
            {
                var rawValue = argumentValue(arg.ParameterName);
                if (rawValue==null && !arg.Optional)
                {
                    throw new ArgumentException($"Missing parameter {arg.ParameterName}!", arg.ParameterName);
                }

                bool nullable = arg.Optional && string.IsNullOrEmpty(arg.DefaultValue);
                rawValue ??= arg.DefaultValue;
                string typeDef;
                if (rawValue is string s && !string.IsNullOrEmpty(s))
                {
                    var paramValue = ParseArgument(s, arg.ParameterType, arg.Format, out typeDef, out var valid);
                    if (valid)
                    {
                        paramWithValue(arg.ParameterName, paramValue, typeDef, nullable);
                    }
                    else
                    {
                        retVal = false;
                        break;
                    }
                    //queryArguments.Add(arg.ParameterName, ParseArgument(arg.DefaultValue, arg.ParameterType, arg.Format, out typeDef));
                }
                else if (rawValue != null)
                {
                    typeDef = GetTypeDef(arg.ParameterType);
                    paramWithValue(arg.ParameterName, rawValue, typeDef, nullable);
                }
                else
                {
                    typeDef = GetTypeDef(arg.ParameterType);
                    paramWithoutValue(arg.ParameterName, typeDef, nullable);
                }
            }

            return retVal;
        }

        public static string GetTypeDef(QueryParameterTypes type)
        {
            switch (type)
            {
                case QueryParameterTypes.Boolean:
                    return "bool";
                case QueryParameterTypes.DateTime:
                    return "DateTime";
                case QueryParameterTypes.Double:
                    return "double";
                case QueryParameterTypes.Int32:
                    return "int";
                case QueryParameterTypes.Int64:
                    return "long";
                case QueryParameterTypes.String:
                    return "string";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        
        public static object ParseArgument(string parameterValue, QueryParameterTypes type, string format, out string typeName, out bool paramValid)
        {
            paramValid = true;
            typeName = "--invalid--";
            object retVal = null;
            switch (type)
            {
                case QueryParameterTypes.Boolean:
                {
                    //retVal = bool.Parse(parameterValue);
                    paramValid = bool.TryParse(parameterValue, out var val);
                    if (paramValid)
                    {
                        typeName = "bool";
                        retVal = val;
                    }

                    break;
                }
                case QueryParameterTypes.DateTime:
                {
                    DateTime val;
                    if (string.IsNullOrEmpty(format))
                    {
                        paramValid = DateTime.TryParse(parameterValue, out val);
                        //retVal = DateTime.Parse(parameterValue);
                    }
                    else
                    {
                        paramValid = DateTime.TryParseExact(parameterValue, format, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out val);
                        //retVal = DateTime.ParseExact(parameterValue, format, CultureInfo.InvariantCulture);
                    }

                    if (paramValid)
                    {
                        retVal = val;
                        typeName = "DateTime";
                    }

                    break;
                }
                case QueryParameterTypes.Double:
                {
                    paramValid = double.TryParse(parameterValue, out var val);
                    //retVal = double.Parse(parameterValue);
                    if (paramValid)
                    {
                        retVal = val;
                        typeName = "double";
                    }

                    break;
                }
                case QueryParameterTypes.Int32:
                {
                    paramValid = int.TryParse(parameterValue, out var val);
                    if (paramValid)
                    {
                        retVal = val;
                        //retVal = int.Parse(parameterValue);
                        typeName = "int";
                    }

                    break;
                }
                case QueryParameterTypes.Int64:
                {
                    paramValid = long.TryParse(parameterValue, out var val);
                    //retVal = long.Parse(parameterValue);
                    if (paramValid)
                    {
                        retVal = val;
                        typeName = "long";
                    }

                    break;
                }
                case QueryParameterTypes.String:
                    retVal = parameterValue;
                    typeName = "string";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            return retVal;
        }

        public class DiagQueryItem
        {
            public IWrappedDataSource Context {get; set; }

            public DiagnosticsQueryDefinition Query { get; set; }
        }

        public class DiagEntityAnlyseItem
        {
            public PropertyInfo Property { get; set; }

            public DiagnosticResultAttribute Attribute{get;set;}
        }
    }
}
