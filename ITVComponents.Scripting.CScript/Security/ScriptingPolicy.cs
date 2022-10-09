using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dynamitey;
using ImpromptuInterface.Build;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Security.Restrictions;

namespace ITVComponents.Scripting.CScript.Security
{
    public class ScriptingPolicy
    {
        private bool locked;
        private PolicyMode policyMode;
        private PolicyMode allowDelegates;
        private PolicyMode globalMethods;
        private PolicyMode scriptMethods;
        private List<IScriptingRestriction> restrictions = new();
        private PolicyMode nativeScripting;
        private PolicyMode typeLoading;
        private PolicyMode createNewInstances;
        public static ScriptingPolicy Default { get; } = new ScriptingPolicy(null).Lock();

        public ScriptingPolicy() : this(null)
        {
        }

        public ScriptingPolicy(ScriptingPolicy other)
        {
            if (other != null)
            {
                Clone(other);
            }
        }

        public PolicyMode PolicyMode
        {
            get => policyMode;
            set
            {
                if (!locked)
                    policyMode = value;
            }
        }

        public PolicyMode AllowDelegates
        {
            get => allowDelegates;
            set
            {
                if (!locked) 
                    allowDelegates = value;
            }
        }

        public PolicyMode GlobalMethods
        {
            get => globalMethods;
            set
            {
                if (!locked)
                    globalMethods = value;
            }
        }

        public PolicyMode ScriptMethods
        {
            get => scriptMethods;
            set
            {
                if (!locked)
                    scriptMethods = value;
            }
        }

        public PolicyMode TypeLoading
        {
            get => typeLoading;
            set
            {
                if (!locked)
                    typeLoading = value;
            }
        }

        public bool Locked => locked;

        public PolicyMode NativeScripting
        {
            get => nativeScripting;
            set
            {
                if (!locked)
                    nativeScripting = value;
            }
        }

        public PolicyMode CreateNewInstances
        {
            get => createNewInstances;
            set
            {
                if (!locked)
                    createNewInstances = value;
            }
        }

        public bool IsDenied(PolicyMode leafMode)
        {
            return leafMode == PolicyMode.Deny ||
                   (PolicyMode == PolicyMode.Deny && leafMode != PolicyMode.Allow);
        }

        public bool IsDenied(EventInfo eventInfo, object target, bool isStatic)
        {
            return CheckAccess(eventInfo, target, isStatic, PolicyMode) == PolicyMode.Deny;
        }

        public bool IsDenied(FieldInfo field, object target, FieldAccessMode accessMode, bool isStatic)
        {
            return CheckAccess(field, target, accessMode, isStatic, PolicyMode) == PolicyMode.Deny;
        }

        public bool IsDenied(PropertyInfo property, object target, PropertyAccessMode accessMode, bool isStatic)
        {
            return CheckAccess(property, target, accessMode, isStatic, PolicyMode) == PolicyMode.Deny;
        }

        public bool IsDenied(MethodInfo method, object target, bool isStatic)
        {
            return CheckAccess(method, target, isStatic, PolicyMode) == PolicyMode.Deny;
        }

        public bool IsDenied(Type targetType, TypeAccessMode accessMode, PolicyMode defaultMode)
        {
            return CheckAccess(targetType, accessMode, defaultMode) == PolicyMode.Deny;
        }
        public bool IsDenied(Assembly assembly, PolicyMode? defaultMode)
        {
            var mode = CheckAccess(PolicyMode, defaultMode ?? PolicyMode.Default);
            return CheckAccess(assembly, mode) == PolicyMode.Deny;
        }

        public ScriptingPolicy Lock()
        {
            locked = true;
            return this;
        }

        private PolicyMode CheckAccess(EventInfo eventInfo, object target, bool isStatic,
            PolicyMode parentAccess = PolicyMode.Default)
        {
            var retVal = parentAccess == PolicyMode.Default ? PolicyMode : parentAccess;
            var targetType = target?.GetType() ?? eventInfo.DeclaringType;
            if (targetType != null)
            {
                retVal = CheckAccess(targetType,
                    isStatic ? TypeAccessMode.StaticEvent : TypeAccessMode.InstanceEvent, retVal);
            }

            if (targetType != eventInfo.DeclaringType)
            {
                retVal = CheckAccess(targetType,
                    isStatic ? TypeAccessMode.StaticEvent : TypeAccessMode.InstanceEvent, retVal);
            }

            var restriction = restrictions.FirstOrDefault(n =>
                n is EventAccessRestriction fr && fr.Event.Equals(eventInfo));
            var propertyMode = restriction?.PolicyMode ?? PolicyMode.Default;
            return CheckAccess(retVal, propertyMode);
        }

        private PolicyMode CheckAccess(FieldInfo field, object target, FieldAccessMode accessMode, bool isStatic,
            PolicyMode parentAccess = PolicyMode.Default)
        {
            var retVal = parentAccess == PolicyMode.Default ? PolicyMode : parentAccess;
            var targetType = target?.GetType() ?? field.DeclaringType;
            if (targetType != null)
            {
                retVal = CheckAccess(targetType,
                    isStatic ? TypeAccessMode.StaticMember: TypeAccessMode.InstanceMember, retVal);
            }

            if (targetType != field.DeclaringType)
            {
                retVal = CheckAccess(targetType, 
                    isStatic ? TypeAccessMode.StaticMember : TypeAccessMode.InstanceMember, retVal);
            }

            var restriction = restrictions.FirstOrDefault(n =>
                n is FieldAccessRestriction fr && fr.Field.Equals(field) &&
                (fr.AccessMode & accessMode) != 0);
            var propertyMode = restriction?.PolicyMode ?? PolicyMode.Default;
            return CheckAccess(retVal, propertyMode);
        }

        private PolicyMode CheckAccess(PropertyInfo property, object target, PropertyAccessMode accessMode, bool isStatic,
            PolicyMode parentAccess = PolicyMode.Default)
        {
            var retVal = parentAccess == PolicyMode.Default ? PolicyMode : parentAccess;
            var targetType = target?.GetType() ?? property.DeclaringType;
            if (targetType != null)
            {
                retVal = CheckAccess(targetType,
                    isStatic ? TypeAccessMode.StaticMember : TypeAccessMode.InstanceMember, retVal);
            }

            if (targetType != property.DeclaringType)
            {
                retVal = CheckAccess(targetType,
                    isStatic ? TypeAccessMode.StaticMember : TypeAccessMode.InstanceMember, retVal);
            }

            var restriction = restrictions.FirstOrDefault(n =>
                n is PropertyAccessRestriction pr && (pr.Property.Equals(property) || MethodHelper.IsDerivedPropertyOf(pr.Property, property)) &&
                (pr.AccessMode & accessMode) != 0);
            var propertyMode = restriction?.PolicyMode ?? PolicyMode.Default;
            return CheckAccess(retVal, propertyMode);
        }

        private PolicyMode CheckAccess(MethodInfo method, object target, bool isStatic,
            PolicyMode parentAccess = PolicyMode.Default)
        {
            var retVal = parentAccess == PolicyMode.Default ? PolicyMode : parentAccess;
            var targetType = target?.GetType() ?? method.DeclaringType;
            if (targetType != null)
            {
                retVal = CheckAccess(targetType,
                    target == null ? TypeAccessMode.StaticMethod :
                    isStatic ? TypeAccessMode.Extension : TypeAccessMode.InstanceMethod, retVal);
            }

            if (targetType != method.DeclaringType)
            {
                retVal = CheckAccess(targetType, TypeAccessMode.StaticMethod, retVal);
            }

            var restriction = this.restrictions.LastOrDefault(n =>
                n is MethodAccessRestriction mr && (mr.Method.Equals(method) || MethodHelper.IsDerivedMethodOf(mr.Method, method)) && mr.PolicyMode != PolicyMode.Default);
            var methodMode = restriction?.PolicyMode??PolicyMode.Default;
            return CheckAccess(retVal, methodMode);
        }

        private PolicyMode CheckAccess(Type targetType, TypeAccessMode accessMode,
            PolicyMode parentAccess = PolicyMode.Default)
        {
            var retVal = parentAccess == PolicyMode.Default ? PolicyMode : parentAccess;
            var assembly = targetType.Assembly;
            retVal = CheckAccess(assembly, retVal);
            var restriction = this.restrictions.LastOrDefault(n =>
                n is TypeAccessRestriction tr && (tr.Type == targetType || tr.Type.IsAssignableFrom(targetType)) && (tr.AccessMode & accessMode) != 0 && tr.PolicyMode != PolicyMode.Default);
            retVal = CheckAccess(retVal, restriction?.PolicyMode ?? PolicyMode.Default);

            if (targetType.IsGenericType)
            {
                var gt = targetType.GetGenericTypeDefinition();
                restriction = this.restrictions.LastOrDefault(n =>
                    n is TypeAccessRestriction tr && (tr.Type == gt || tr.Type.IsAssignableFrom(gt)) && (tr.AccessMode & accessMode) != 0 && tr.PolicyMode != PolicyMode.Default);
                retVal = CheckAccess(retVal, restriction?.PolicyMode ?? PolicyMode.Default);
            }

            return retVal;
        }

        private PolicyMode CheckAccess(Assembly assembly, PolicyMode parentAccess = PolicyMode.Default)
        {
            parentAccess = parentAccess == PolicyMode.Default ? PolicyMode : parentAccess;
            var an = assembly.GetName();
            var restriction = restrictions.FirstOrDefault(n =>
                n is AssemblyRestriction ar && (ar.AssemblyName.Equals(an.Name) || ar.AssemblyName.Equals(an.FullName)));
            var assemblyMode = restriction?.PolicyMode??PolicyMode.Default;
            return CheckAccess(parentAccess, assemblyMode);
        }

        private PolicyMode CheckAccess(PolicyMode parentAccess, PolicyMode innerAccess)
        {
            if (innerAccess == PolicyMode.Default)
                return parentAccess != PolicyMode.Default ? parentAccess : PolicyMode.Allow;
            return innerAccess;
        }

        private void Clone(ScriptingPolicy other)
        {
            policyMode = other.PolicyMode;
            allowDelegates = other.AllowDelegates;
            globalMethods = other.GlobalMethods;
            scriptMethods = other.ScriptMethods;
            createNewInstances = other.CreateNewInstances;
            nativeScripting = other.NativeScripting;
            typeLoading = other.typeLoading;
            restrictions.AddRange(from t in other.restrictions select t.Clone());
        }

        public void AddPolicy(IScriptingRestriction restriction)
        {
            if (!locked)
            {
                restrictions.Add(restriction);
            }
        }
    }
}
