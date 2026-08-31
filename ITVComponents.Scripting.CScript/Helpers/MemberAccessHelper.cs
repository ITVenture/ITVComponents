using System;
using System.Reflection;
using ITVComponents.MemberAccess;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Helpers
{
    /// <summary>
    /// The script-side of the member-access: everything that is interpreter-semantics, on top of
    /// the shared <see cref="MemberAccessor"/>.
    /// </summary>
    /// <remarks>
    /// The matrix (find a member, read and write properties, fields, dictionaries) lives in
    /// ITVComponents so that a path means the same thing in a script and in a form. What stays
    /// here is what only a script has: object- and function-literals, '$Type', event-subscription
    /// and the exception-types the interpreter raises.
    /// </remarks>
    internal static class MemberAccessHelper
    {
        /// <summary>
        /// The synthetic member that yields the Type an expression stands for.
        /// </summary>
        /// <remarks>
        /// The '$' is deliberate: C# identifiers can not contain it, so this name can never
        /// shadow a real member.
        /// </remarks>
        internal const string TypeMemberName = "$Type";

        /// <param name="instanceSemantics">
        /// when the target is a Type: whether to access the members of the Type-object itself
        /// instead of the static members of the class it stands for. Set by a preceding
        /// '$Type'.
        /// </param>
        public static object GetMemberValue(this object target, string name, Type explicitType, ScriptValues.ValueType valueType, ScriptingPolicy policy, MemberAccessMode mode, bool instanceSemantics = false)
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

            if (name == TypeMemberName)
            {
                // Yields the bare Type-object, no wrapper: from here the value travels into
                // variables, arguments and comparisons, and the unwrapping done for transient
                // values (TypedNull, ReferenceWrapper) only covers method-arguments. A wrapper
                // would be a foreign body everywhere else.
                // What switches the strategy is not this value but the access that follows it.
                return mode == MemberAccessMode.Read ? TypeOf(target) : (object)true;
            }

            if (target == null)
            {
                throw new ScriptException(string.Format("Unable to access {0} on a NULL - Value", name));
            }

            var slot = MemberAccessor.Resolve(target, name, explicitType,
                ScriptingPolicyMemberAccessGuard.For(policy), instanceSemantics);
            var targetObject = slot.Target;
            if (slot.Member == null && slot.Kind != MemberSlotKind.EnumValue)
            {
                if (targetObject is ObjectLiteral ojl)
                {
                    return mode == MemberAccessMode.Read ? ojl[name] : ojl.ContainsKey(name);
                }

                if (targetObject is FunctionLiteral ful)
                {
                    return mode == MemberAccessMode.Read
                        ? ful.GetInitialScopeValue(name)
                        : ful.InitialScopeValueExists(name);
                }

                if (slot.Kind != MemberSlotKind.None)
                {
                    // a dictionary or another carrier: an absent name is empty, not an error
                    if (mode == MemberAccessMode.Read)
                    {
                        slot.TryRead(out var carried);
                        return carried;
                    }

                    return slot.Exists();
                }
            }

            if (mode == MemberAccessMode.CheckExists)
            {
                return slot.Exists();
            }

            var result = slot.TryRead(out var value);
            switch (result)
            {
                case MemberAccessResult.Ok:
                    return value;
                case MemberAccessResult.NotReadable:
                    return null;
                case MemberAccessResult.Denied:
                    throw new ScriptSecurityException(DenialMessage(slot));
                case MemberAccessResult.NotFound:
                    throw new ScriptException(string.Format("Member {0} is not declared on {1}", name,
                        targetObject));
                default:
                    throw new ScriptException(string.Format("GetValue is not supported for MemberType {0}",
                        slot.Member.MemberType));
            }
        }

        public static Type GetMemberType(this object target, string name, Type explicitType, ScriptValues.ValueType valueType)
        {
            if (valueType == ScriptValues.ValueType.Method || valueType == ScriptValues.ValueType.Constructor)
            {
                return null;
            }

            if (target == null)
            {
                throw new ScriptException(string.Format("Unable to access {0} on a NULL - Value", name));
            }

            var slot = MemberAccessor.Resolve(target, name, explicitType);
            var targetObject = slot.Target;
            if (slot.Member == null && slot.Kind != MemberSlotKind.EnumValue)
            {
                if (targetObject is ObjectLiteral ojl)
                {
                    return ojl[name]?.GetType()??typeof(object);
                }

                if (targetObject is FunctionLiteral ful)
                {
                    return ful.GetInitialScopeValue(name)?.GetType() ?? typeof(object);
                }

                if (slot.Kind != MemberSlotKind.None)
                {
                    return slot.GetMemberType();
                }
            }

            if (slot.Kind == MemberSlotKind.None)
            {
                throw new ScriptException(string.Format("Member {0} is not declared on {1}", name,
                                                        targetObject));
            }

            if (slot.Kind == MemberSlotKind.Unsupported)
            {
                throw new ScriptException(string.Format("GetValue is not supported for MemberType {0}",
                    slot.Member.MemberType));
            }

            return slot.GetMemberType();
        }

        public static void SetMemberValue(this object target, string name, object value, Type explicitType, ScriptValues.ValueType valueType, ScriptingPolicy policy)
        {
            if (target == null)
            {
                throw new ScriptException(string.Format("Unable to access {0} on a NULL - Value", name));
            }

            var slot = MemberAccessor.Resolve(target, name, explicitType,
                ScriptingPolicyMemberAccessGuard.For(policy));
            var targetObject = slot.Target;
            if (slot.Member == null)
            {
                if (targetObject is ObjectLiteral ojl)
                {
                    ojl[name] = value;
                    return;
                }

                if (targetObject is FunctionLiteral ful)
                {
                    ful.SetInitialScopeValue(name, value);
                    return;
                }

                if (slot.Kind == MemberSlotKind.Dictionary || slot.Kind == MemberSlotKind.MemberSource)
                {
                    slot.Write(value);
                    return;
                }

                throw new ScriptException(string.Format("Member {0} is not declared on {1}", name,
                    targetObject));
            }

            // Subscribing to an event is script-semantics: it turns a function-literal into a
            // delegate, which is nothing the generic accessor knows about.
            if (slot.Kind == MemberSlotKind.Event && value is FunctionLiteral fl)
            {
                var ev = (EventInfo)slot.Member;
                if (policy.IsDenied(policy.ScriptMethods))
                {
                    throw new ScriptSecurityException("Scriptmethods are not allowed");
                }

                if (policy.IsDenied(ev, targetObject, targetObject == null))
                {
                    throw new ScriptSecurityException($"Access to event '{ev.Name}' is denied");
                }

                ev.AddEventHandler(targetObject, fl.CreateDelegate(ev.EventHandlerType));
                return;
            }

            var result = slot.TryWrite(value);
            if (result == MemberAccessResult.Ok)
            {
                return;
            }

            if (result == MemberAccessResult.Denied)
            {
                throw new ScriptSecurityException(DenialMessage(slot));
            }

            throw new ScriptException(string.Format("SetValue is not supported for this Member ({0}", name));
        }

        /// <summary>
        /// Builds the message for an access the policy has refused
        /// </summary>
        /// <param name="slot">the slot whose access was refused</param>
        /// <returns>the message for the refusal</returns>
        private static string DenialMessage(MemberSlot slot)
        {
            switch (slot.Kind)
            {
                case MemberSlotKind.Property:
                    return $"Access to property '{slot.Member.Name}' is denied";
                case MemberSlotKind.Field:
                    return $"Access to field '{slot.Member.Name}' is denied";
                case MemberSlotKind.EnumValue:
                    return $"Access to enum '{slot.EnumType.FullName}' is denied";
                default:
                    return $"Access to member '{slot.Name}' is denied";
            }
        }

        /// <summary>
        /// Gets the type an expression stands for: a Type stands for itself, anything else for
        /// its runtime-type.
        /// </summary>
        private static Type TypeOf(object target)
        {
            return target as Type ?? target?.GetType();
        }
    }

    internal enum MemberAccessMode
    {
        Read,
        CheckExists
    }
}
