using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core.Invokation;
#if (!Community)
using ITVComponents.ExtendedFormatting;
#endif
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Optimization;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;

namespace ITVComponents.Scripting.CScript.Helpers
{
    internal static class MemberAccessHelper
    {

        public static object GetIndexOnValue(object rawValue, object[] arguments, Type explicitType, bool useLazyEvaluation, ref IExecutor lazyExecutor)
        {
            if (arguments == null || arguments.Length == 0)
            {
                if (rawValue is InvokationHelper invokationHelper)
                {
                    bool ok;
                    object retVal = invokationHelper.Invoke(null, out ok);
                    if (ok)
                    {
                        return retVal;
                    }
                }

                if (rawValue is FunctionLiteral fx && fx.AutoInvokeEnabled)
                {
                    return fx.Invoke(null);
                }

                return rawValue;
            }

            if (rawValue == null)
                {
                    throw new ScriptException("Indexer Failed for NULL - Value");
                }

                if (rawValue is Type)
                {
                    throw new ScriptException("Indexer call for Types not supported");
                }

                
                if (!(rawValue is Array))
                {
                    object[] args;
                    Type targetType = rawValue.GetType();
                    if (explicitType != null)
                    {
                        if (!explicitType.IsAssignableFrom(targetType))
                        {
                            throw new ScriptException("Provided Type is not implemented by the target-object");
                        }

                        targetType = explicitType;
                    }

                    PropertyInfo pi = MethodHelper.GetCapableIndexer(targetType,
                                                                     arguments,
                                                                     out args);
                    if (pi == null)
                    {
                        throw new ScriptException("No capable Indexer found for the provided arguments");
                    }

                    if (useLazyEvaluation)
                    {
                        lazyExecutor = new LazyIndexer(pi,arguments.Length != args.Length);
                    }

                    return pi.GetValue(rawValue, args);
                }

                Array arr = (Array)rawValue;
                return arr.GetValue(arguments.Cast<int>().ToArray());
        }

        public static object GetMemberValue(this object target, string name, Type explicitType, ValueType valueType)
        {
            if (valueType== ValueType.Method || valueType == ValueType.Constructor)
            {
                var bv = target;
                var olt = bv as ObjectLiteral;
                if (valueType == ValueType.Constructor && olt != null)
                {
                    return olt[name];
                }

                return bv;
            }

            object targetObject;
            bool isEnum;
            MemberInfo mi = FindMember(target, name, explicitType, out targetObject, out isEnum);
            ObjectLiteral ojl = targetObject as ObjectLiteral;
            FunctionLiteral ful = targetObject as FunctionLiteral;
            IDictionary<string, object> odi = targetObject as IDictionary<string, object>;
#if (!Community)
            IBasicKeyValueProvider iba = targetObject as IBasicKeyValueProvider;
#endif
            if (mi == null)
            {
                if (ojl != null)
                {
                    return ojl[name];
                }

                if (ful != null)
                {
                    return ful.GetInitialScopeValue(name);
                }

                if (odi != null && odi.ContainsKey(name))
                {
                    return odi[name];
                }
                else if (odi != null)
                {
                    return null;
                }

#if(!Community)
                if (iba != null && iba.ContainsKey(name))
                {
                    return iba[name];
                }
                else if (iba != null)
                {
                    return null;
                }
#endif
            }

            if (
                isEnum)
            {
                return Enum.Parse((Type)targetObject, name);
            }

            if (mi == null)
            {
                throw new ScriptException(string.Format("Member {0} is not declared on {1}", name,
                                                        targetObject));
            }

            if (mi is PropertyInfo)
            {
                PropertyInfo pi = (PropertyInfo)mi;
                if (pi.CanRead)
                {
                    return pi.GetValue(targetObject, null);
                }

                return null;
            }

            if (mi is FieldInfo)
            {
                return ((FieldInfo)mi).GetValue(targetObject);
            }

            if (mi is EventInfo)
            {
                return null;
            }

            throw new ScriptException(string.Format("GetValue is not supported for MemberType {0}", mi.MemberType));
        }

        public static void SetMemberValue(this object target, string name, object value, Type explicitType, ValueType valueType)
        {
            object targetObject;
            bool isEnum;
            MemberInfo mi = FindMember(target,name, explicitType, out targetObject, out isEnum);
            ObjectLiteral ojl = targetObject as ObjectLiteral;
            FunctionLiteral ful = targetObject as FunctionLiteral;
            IDictionary<string, object> odi = targetObject as IDictionary<string, object>;
            if (mi == null)
            {
                if (ojl != null)
                {
                    ojl[name] = value;
                    return;
                }

                if (ful != null)
                {
                    ful.SetInitialScopeValue(name, value);
                    return;
                }

                if (odi != null)
                {
                    odi[name] = value;
                    return;
                }

                throw new ScriptException(string.Format("Member {0} is not declared on {1}", name,
                    targetObject));
            }

            PropertyInfo pi;
            FieldInfo fi;
            if (mi is PropertyInfo && (pi = (PropertyInfo)mi).CanWrite)
            {
                pi.SetValue(targetObject, value, null);
            }
            else if (mi is FieldInfo && !(fi = (FieldInfo)mi).IsLiteral)
            {
                fi.SetValue(targetObject, value);
            }
            else if (mi is EventInfo && value is FunctionLiteral)
            {
                FunctionLiteral fl = value as FunctionLiteral;
                EventInfo ev = mi as EventInfo;
                ev.AddEventHandler(targetObject, fl.CreateDelegate(ev.EventHandlerType));
            }
            else
            {
                throw new ScriptException(string.Format("SetValue is not supported for this Member ({0}", name));
            }
        }

        /// <summary>
        /// Finds the member with the given Name
        /// </summary>
        /// <param name="targetObject">the target object from which to read the value of the returned member</param>
        /// <param name="isEnum">indicates whether the base object is an enum type</param>
        /// <returns>a memberinfo that represents the name of this memberAccessValue object</returns>
        private static MemberInfo FindMember(object baseVal, string memberName, Type explicitTargetType, out object targetObject, out bool isEnum)
        {
            if (baseVal == null)
            {
                throw new ScriptException(string.Format("Unable to access {0} on a NULL - Value", memberName));
            }

            targetObject = baseVal;
            isEnum = false;
            bool isStatic = false;
            if (baseVal is Type)
            {
                targetObject = null;
                isStatic = true;
                if (((Type)baseVal).IsEnum)
                {
                    targetObject = baseVal;
                    isEnum = true;
                    return null;
                }
            }
            else
            {
                baseVal = explicitTargetType ?? baseVal.GetType();
            }

            Type t = (Type)baseVal;
            return (from m in t.GetMembers(BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance)) where m.Name == memberName select m).FirstOrDefault();
        }
    }

    /// <summary>
    /// Enum for the Supported Value Types in a Script
    /// </summary>
    public enum ValueType
    {
        /// <summary>
        /// The value is literal and can not be assigned
        /// </summary>
        Literal,

        /// <summary>
        /// The Value is a Property and can be read and written
        /// </summary>
        PropertyOrField,

        /// <summary>
        /// The Value is a Method and can only be read
        /// </summary>
        Method,

        /// <summary>
        /// The Value is a Constructor and can only be read
        /// </summary>
        Constructor
    }
}
