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

        public static IDictionary<string, object> VerifyArguments(DiagnosticsQuery query, IDictionary<string, object> arguments)
        {
            var retVal = new Dictionary<string, object>();
            VerifyArguments(query, arguments, (name, value, type, nullable) => retVal.Add(name, value), (name, type, nullable) => retVal.Add(name, DBNull.Value));
            return retVal;
        }
        
        public static IDictionary<string, object> BuildArguments(DiagnosticsQuery query, IDictionary<string, string> arguments)
        {
            var retVal = new Dictionary<string, object>();
            BuildArguments(query, arguments, (name, value, type, nullable) => retVal.Add(name, value), (name, type, nullable) => retVal.Add(name, DBNull.Value));
            return retVal;
        }
        
        public static void VerifyArguments(DiagnosticsQuery query, IDictionary<string, object> queryArguments, StringBuilder fullQuery)
        {
            var writeParam = new Action<string, string, bool>((name, type, nullable) =>
            {
                fullQuery.AppendLine($"{type}{(nullable ? "?" : "")} {name} = Global.{name};");
            });

            VerifyArguments(query, queryArguments, (name, value, type, nullable) =>
            {
                if (!queryArguments.ContainsKey(name))
                {
                    queryArguments.Add(name, value);
                }

                writeParam(name, type, nullable);
            }, writeParam);
        }

        private static void VerifyArguments(DiagnosticsQuery query, IDictionary<string, object> rawArguments, Action<string, object, string, bool> paramWithValue, Action<string, string, bool> paramWithoutValue)
        {
            ProcessArguments(query, s =>
            {
                object retVal = null;
                if (rawArguments.ContainsKey(s))
                {
                    retVal = rawArguments[s];
                }

                return retVal;
            }, paramWithValue, paramWithoutValue);
        }

        public static void BuildArguments(DiagnosticsQuery query, IDictionary<string, string> arguments, IDictionary<string, object> queryArguments, StringBuilder fullQuery)
        {
            var writeParam = new Action<string, string, bool>((name, type, nullable) =>
            {
                fullQuery.AppendLine($"{type}{(nullable ? "?" : "")} {name} = Global.{name};");
            });

            BuildArguments(query, arguments, (name, value, type, nullable) =>
            {
                if (!queryArguments.ContainsKey(name))
                {
                    queryArguments.Add(name, value);
                }

                writeParam(name, type, nullable);
            }, writeParam);
        }

        private static void BuildArguments(DiagnosticsQuery query, IDictionary<string, string> arguments, Action<string, object, string, bool> paramWithValue, Action<string, string, bool> paramWithoutValue)
        {
            ProcessArguments(query, s =>
            {
                object retVal = null;
                if (arguments.ContainsKey(s))
                {
                    retVal = arguments[s];
                }

                return retVal;
            }, paramWithValue, paramWithoutValue);
        }

        private static void ProcessArguments(DiagnosticsQuery query, Func<string, object> argumentValue, Action<string, object, string, bool> paramWithValue, Action<string, string, bool> paramWithoutValue)
        {
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
                    var paramValue = ParseArgument(s, arg.ParameterType, arg.Format, out typeDef);
                    paramWithValue(arg.ParameterName, paramValue, typeDef, nullable);
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
        
        public static object ParseArgument(string parameterValue, QueryParameterTypes type, string format, out string typeName)
        {
            object retVal;
            switch (type)
            {
                case QueryParameterTypes.Boolean:
                    retVal = bool.Parse(parameterValue);
                    typeName = "bool";
                    break;
                case QueryParameterTypes.DateTime:
                    if (string.IsNullOrEmpty(format))
                    {
                        retVal = DateTime.Parse(parameterValue);
                    }
                    else
                    {
                        retVal = DateTime.ParseExact(parameterValue, format, CultureInfo.InvariantCulture);
                    }

                    typeName = "DateTime";
                    break;
                case QueryParameterTypes.Double:
                    retVal = double.Parse(parameterValue);
                    typeName = "double";
                    break;
                case QueryParameterTypes.Int32:
                    retVal = int.Parse(parameterValue);
                    typeName = "int";
                    break;
                case QueryParameterTypes.Int64:
                    retVal = long.Parse(parameterValue);
                    typeName = "long";
                    break;
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

            public DiagnosticsQuery Query { get; set; }
        }

        public class DiagEntityAnlyseItem
        {
            public PropertyInfo Property { get; set; }

            public DiagnosticResultAttribute Attribute{get;set;}
        }
    }
}
