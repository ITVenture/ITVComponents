using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;
#if (!Community)
using ITVComponents.ExtendedFormatting;
#endif
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Restrictions;

namespace ITVComponents.Scripting.CScript.Helpers
{
    internal static class MemberAccessHelper
    {
        public static object GetMemberValue(this object target, string name, Type explicitType, ScriptValues.ValueType valueType, ScriptingPolicy policy, MemberAccessMode mode)
        {
            if (valueType== ScriptValues.ValueType.Method || valueType == ScriptValues.ValueType.Constructor)
            {
                var bv = target;
                if (valueType == ScriptValues.ValueType.Constructor && bv is ObjectLiteral olt)
                {
                    return mode == MemberAccessMode.Read ? olt[name] : olt.ContainsKey(name);
                }

                return mode == MemberAccessMode.Read ? bv : true;
            }

            object targetObject;
            bool isEnum;
            MemberInfo mi = FindMember(target, name, explicitType, out targetObject, out isEnum);
            ObjectLiteral ojl = targetObject as ObjectLiteral;
            FunctionLiteral ful = targetObject as FunctionLiteral;
            IDictionary<string, object> odi = targetObject as IDictionary<string, object>;
            IBasicKeyValueProvider iba = targetObject as IBasicKeyValueProvider;
            if (mi == null)
            {
                if (ojl != null)
                {
                    return mode == MemberAccessMode.Read ? ojl[name] : ojl.ContainsKey(name);
                }

                if (ful != null)
                {
                    return mode == MemberAccessMode.Read
                        ? ful.GetInitialScopeValue(name)
                        : ful.InitialScopeValueExists(name);
                }

                if (odi != null && odi.ContainsKey(name))
                {
                    return mode == MemberAccessMode.Read ? odi[name] : true;
                }
                else if (odi != null)
                {
                    return mode == MemberAccessMode.Read ? null : false;
                }

                if (iba != null && iba.ContainsKey(name))
                {
                    return mode == MemberAccessMode.Read ? iba[name] : true;
                }
                else if (iba != null)
                {
                    return mode == MemberAccessMode.Read ? null : false;
                }
            }

            if (isEnum)
            {
                if (policy.IsDenied((Type)targetObject, TypeAccessMode.Direct, policy.PolicyMode))
                {
                    if (mode == MemberAccessMode.CheckExists)
                    {
                        return false;
                    }

                    throw new ScriptSecurityException($"Access to enum '{((Type)targetObject).FullName}' is denied");
                }

                var ok = Enum.TryParse((Type)targetObject, name, out var rem);
                return mode == MemberAccessMode.Read ? rem : ok;
            }

            if (mi == null && mode == MemberAccessMode.Read)
            {
                throw new ScriptException(string.Format("Member {0} is not declared on {1}", name,
                                                        targetObject));
            }
            else if (mi == null)
            {
                return false;
            }

            if (mi is PropertyInfo pi)
            {
                bool denied = policy.IsDenied(pi, targetObject, PropertyAccessMode.Read, targetObject == null);
                if (denied && mode == MemberAccessMode.Read)
                {
                    throw new ScriptSecurityException($"Access to property '{pi.Name}' is denied");
                }
                else if (denied)
                {
                    return false;
                }

                if (pi.CanRead && mode == MemberAccessMode.Read)
                {
                    return pi.GetValue(targetObject, null);
                }
                
                if (mode == MemberAccessMode.CheckExists)
                {
                    return pi.CanRead;
                }

                return null;
            }

            if (mi is FieldInfo fi)
            {
                var denied = policy.IsDenied(fi, targetObject, FieldAccessMode.Read, targetObject == null);
                if (denied && mode == MemberAccessMode.Read)
                {
                    throw new ScriptSecurityException($"Access to field '{fi.Name}' is denied");
                }
                else if (denied)
                {
                    return false;
                }

                return mode == MemberAccessMode.Read ? fi.GetValue(targetObject) : true;
            }

            if (mi is EventInfo)
            {
                return mode == MemberAccessMode.Read ? null : true;
            }

            if (mode == MemberAccessMode.Read)
            {
                throw new ScriptException(string.Format("GetValue is not supported for MemberType {0}", mi.MemberType));
            }

            return false;
        }

        public static Type GetMemberType(this object target, string name, Type explicitType, ScriptValues.ValueType valueType)
        {
            if (valueType == ScriptValues.ValueType.Method || valueType == ScriptValues.ValueType.Constructor)
            {
                return null;
            }

            object targetObject;
            bool isEnum;
            MemberInfo mi = FindMember(target, name, explicitType, out targetObject, out isEnum);
            ObjectLiteral ojl = targetObject as ObjectLiteral;
            FunctionLiteral ful = targetObject as FunctionLiteral;
            IDictionary<string, object> odi = targetObject as IDictionary<string, object>;
            IBasicKeyValueProvider iba = targetObject as IBasicKeyValueProvider;
            if (mi == null)
            {
                if (ojl != null)
                {
                    return ojl[name]?.GetType()??typeof(object);
                }

                if (ful != null)
                {
                    return ful.GetInitialScopeValue(name)?.GetType() ?? typeof(object);
                }

                if (odi != null && odi.ContainsKey(name))
                {
                    return odi[name]?.GetType() ?? typeof(object);
                }
                else if (odi != null)
                {
                    return null;
                }

                if (iba != null && iba.ContainsKey(name))
                {
                    return iba[name]?.GetType() ?? typeof(object);
                }
                else if (iba != null)
                {
                    return null;
                }
            }

            if (isEnum)
            {
                return (Type)targetObject;
            }

            if (mi == null)
            {
                throw new ScriptException(string.Format("Member {0} is not declared on {1}", name,
                                                        targetObject));
            }

            if (mi is PropertyInfo pi)
            {
                if (pi.CanRead)
                {
                    return pi.PropertyType;
                }

                return null;
            }

            if (mi is FieldInfo fi)
            {
                return fi.FieldType;
            }

            if (mi is EventInfo ev)
            {
                return ev.EventHandlerType;
            }

            throw new ScriptException(string.Format("GetValue is not supported for MemberType {0}", mi.MemberType));
        }

        public static void SetMemberValue(this object target, string name, object value, Type explicitType, ScriptValues.ValueType valueType, ScriptingPolicy policy)
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

            if (mi is PropertyInfo{CanWrite:true} pi)
            {
                if (policy.IsDenied(pi, targetObject, PropertyAccessMode.Write, targetObject == null))
                {
                    throw new ScriptSecurityException($"Access to property '{pi.Name}' is denied");
                }

                pi.SetValue(targetObject, value, null);
            }
            else if (mi is FieldInfo{IsLiteral:false} fi)
            {
                if (policy.IsDenied(fi, targetObject, FieldAccessMode.Write, targetObject == null))
                {
                    throw new ScriptSecurityException($"Access to field '{fi.Name}' is denied");
                }

                fi.SetValue(targetObject, value);
            }
            else if (mi is EventInfo ev && value is FunctionLiteral fl)
            {
                if (policy.IsDenied(policy.ScriptMethods))
                {
                    throw new ScriptSecurityException("Scriptmethods are not allowed");
                }

                if (policy.IsDenied(ev, targetObject, targetObject == null))
                {
                    throw new ScriptSecurityException($"Access to event '{ev.Name}' is denied");
                }

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

    internal enum MemberAccessMode
    {
        Read,
        CheckExists
    }
}
