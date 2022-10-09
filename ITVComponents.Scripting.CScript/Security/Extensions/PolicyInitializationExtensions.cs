using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Annotations;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security.Restrictions;

namespace ITVComponents.Scripting.CScript.Security.Extensions
{
    public static class PolicyInitializationExtensions
    {
        public static ScriptingPolicy Configure(this ScriptingPolicy policy, Action<ScriptingPolicy> configure)
        {
            var retVal = AsModifyable(policy);
            configure?.Invoke(retVal);
            return retVal;
        }

        public static ScriptingPolicy WithAssemblyRestriction(this ScriptingPolicy policy, Type containingType,
            PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new AssemblyRestriction
            {
                AssemblyName = containingType.Assembly.FullName,
                PolicyMode = policyMode
            });

            return retVal;
        }

        public static ScriptingPolicy WithAssemblyRestriction(this ScriptingPolicy policy, string assemblyName,
            PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new AssemblyRestriction
            {
                AssemblyName = assemblyName,
                PolicyMode = policyMode
            });

            return retVal;
        }

        public static ScriptingPolicy WithEventAccessRestriction<T>(this ScriptingPolicy policy, string eventName, bool instance,
            PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new EventAccessRestriction
            {
                Event= LambdaHelper.GetEventInfo<T>(eventName,instance),
                PolicyMode = policyMode
            });

            return retVal;
        }

        public static ScriptingPolicy WithEventAccessRestriction(this ScriptingPolicy policy, Type t, string eventName, bool instance,
            PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new EventAccessRestriction
            {
                Event = LambdaHelper.GetEventInfo(t,eventName, instance),
                PolicyMode = policyMode
            });

            return retVal;
        }

        public static ScriptingPolicy WithFieldAccessRestriction<T>(this ScriptingPolicy policy,
            Expression<Func<T, object>> fieldAccess, FieldAccessMode accessMode, PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            var fieldInfo = LambdaHelper.GetFieldInfo<T>(fieldAccess);
            retVal.AddPolicy(new FieldAccessRestriction
            {
                Field = fieldInfo,
                PolicyMode = policyMode,
                AccessMode =accessMode
            });
            var reverse = policyMode == PolicyMode.Deny ? PolicyMode.Allow : policyMode == PolicyMode.Allow ? PolicyMode.Deny : PolicyMode.Default;
            var reverseAccess = ~accessMode;
            if ((reverseAccess & FieldAccessMode.Any) != 0)
            {
                retVal.AddPolicy(new FieldAccessRestriction
                {
                    Field = fieldInfo,
                    PolicyMode = reverse,
                    AccessMode = reverseAccess
                });
            }

            return retVal;
        }

        public static ScriptingPolicy WithFieldAccessRestriction(this ScriptingPolicy policy,
            Expression<Func<object>> fieldAccess, FieldAccessMode accessMode, PolicyMode policyMode)
        {
            var fieldInfo = LambdaHelper.GetFieldInfo(fieldAccess);
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new FieldAccessRestriction
            {
                Field = fieldInfo,
                PolicyMode = policyMode,
                AccessMode = accessMode
            });
            var reverse = policyMode == PolicyMode.Deny ? PolicyMode.Allow : policyMode == PolicyMode.Allow ? PolicyMode.Deny : PolicyMode.Default;
            var reverseAccess = ~accessMode;
            if ((reverseAccess & FieldAccessMode.Any) != 0)
            {
                retVal.AddPolicy(new FieldAccessRestriction
                {
                    Field = fieldInfo,
                    PolicyMode = reverse,
                    AccessMode = reverseAccess
                });
            }

            return retVal;
        }

        public static ScriptingPolicy WithMethodAccessRestriction<T>(this ScriptingPolicy policy,
            Expression<Func<T, object>> methodAccess, PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new MethodAccessRestriction
            {
                Method = LambdaHelper.GetMethodInfo<T>(methodAccess),
                PolicyMode = policyMode
            });

            return retVal;
        }

        public static ScriptingPolicy WithMethodAccessRestriction(this ScriptingPolicy policy,
            Expression<Func<object>> methodAccess, PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new MethodAccessRestriction
            {
                Method = LambdaHelper.GetMethodInfo(methodAccess),
                PolicyMode = policyMode
            });

            return retVal;
        }

        public static ScriptingPolicy WithMethodAccessRestriction<T>(this ScriptingPolicy policy,
            Expression<Action<T>> methodAccess, PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new MethodAccessRestriction
            {
                Method = LambdaHelper.GetMethodInfo<T>(methodAccess),
                PolicyMode = policyMode
            });

            return retVal;
        }

        public static ScriptingPolicy WithMethodAccessRestriction(this ScriptingPolicy policy,
            Expression<Action> methodAccess, PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new MethodAccessRestriction
            {
                Method = LambdaHelper.GetMethodInfo(methodAccess),
                PolicyMode = policyMode
            });

            return retVal;
        }

        public static ScriptingPolicy WithPropertyAccessRestriction<T>(this ScriptingPolicy policy,
            Expression<Func<T, object>> propertyAccess, PropertyAccessMode accessMode, PolicyMode policyMode)
        {
            var property = LambdaHelper.GetPropertyInfo<T>(propertyAccess);
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new PropertyAccessRestriction
            {
                Property = property,
                PolicyMode = policyMode,
                AccessMode = accessMode
            });
            var reverse = policyMode == PolicyMode.Deny ? PolicyMode.Allow : policyMode == PolicyMode.Allow ? PolicyMode.Deny : PolicyMode.Default;
            var reverseAccess = ~accessMode;
            if ((reverseAccess & PropertyAccessMode.Any) != 0)
            {
                retVal.AddPolicy(new PropertyAccessRestriction
                {
                    Property = property,
                    PolicyMode = reverse,
                    AccessMode = reverseAccess
                });
            }

            return retVal;
        }

        public static ScriptingPolicy WithPropertyAccessRestriction(this ScriptingPolicy policy,
            Expression<Func<object>> propertyAccess, PropertyAccessMode accessMode, PolicyMode policyMode)
        {
            var property = LambdaHelper.GetPropertyInfo(propertyAccess);
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new PropertyAccessRestriction
            {
                Property = property,
                PolicyMode = policyMode,
                AccessMode = accessMode
            });
            var reverse = policyMode == PolicyMode.Deny ? PolicyMode.Allow : policyMode == PolicyMode.Allow ? PolicyMode.Deny : PolicyMode.Default;
            var reverseAccess = ~accessMode;
            if ((reverseAccess & PropertyAccessMode.Any) != 0)
            {
                retVal.AddPolicy(new PropertyAccessRestriction
                {
                    Property = property,
                    PolicyMode = reverse,
                    AccessMode = reverseAccess
                });
            }

            return retVal;
        }

        public static ScriptingPolicy WithTypeRestriction(this ScriptingPolicy policy,
            Type t, TypeAccessMode accessMode, PolicyMode policyMode)
        {
            var retVal = AsModifyable(policy);
            retVal.AddPolicy(new TypeAccessRestriction
            {
                Type= t,
                PolicyMode = policyMode,
                AccessMode = accessMode
            });
            var reverse = policyMode == PolicyMode.Deny?PolicyMode.Allow:policyMode == PolicyMode.Allow?PolicyMode.Deny:PolicyMode.Default;
            var reverseAccess = ~accessMode;
            if ((reverseAccess & TypeAccessMode.Any) != 0)
            {
                retVal.AddPolicy(new TypeAccessRestriction
                {
                    Type = t,
                    PolicyMode = reverse,
                    AccessMode = ~accessMode
                });
            }

            return retVal;
        }

        private static ScriptingPolicy AsModifyable(ScriptingPolicy original)
        {
            if (original.Locked)
            {
                return new ScriptingPolicy(original);
            }

            return original;
        }
    }
}
