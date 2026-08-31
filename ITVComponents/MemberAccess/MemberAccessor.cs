using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.MemberAccess
{
    /// <summary>
    /// Resolves a name on an object into a <see cref="MemberSlot"/> that can be read and written.
    /// </summary>
    /// <remarks>
    /// This is the shared core behind the member-access of the script-interpreter and the
    /// field-paths of the workflow-forms. It exists exactly once so that a.b.c can not come to
    /// mean two different things in the two places.
    /// </remarks>
    public static class MemberAccessor
    {
        /// <summary>
        /// Resolves a name on the given target
        /// </summary>
        /// <param name="target">the object on which to resolve the name</param>
        /// <param name="name">the name to resolve</param>
        /// <param name="explicitType">an explicit type to look the name up on, instead of the runtime-type of the target</param>
        /// <param name="guard">the guard that decides whether the access may happen; null allows everything</param>
        /// <param name="instanceSemantics">
        /// when the target is a Type: whether to access the members of the Type-object itself
        /// instead of the static members of the class it stands for
        /// </param>
        /// <returns>a slot describing what was found; never null</returns>
        public static MemberSlot Resolve(object target, string name, Type explicitType = null,
            IMemberAccessGuard guard = null, bool instanceSemantics = false)
        {
            if (target == null)
            {
                throw new MemberAccessFailedException($"Unable to access {name} on a NULL - Value");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            object targetObject = target;
            var isStatic = false;
            Type lookupType;

            // A Type normally stands for its class, so members are looked up statically. When the
            // caller asks for instance-semantics, the Type-object itself is meant - it is then
            // treated like any other object, which is exactly what skipping this branch achieves.
            if (target is Type type && !instanceSemantics)
            {
                targetObject = null;
                isStatic = true;
                if (type.IsEnum)
                {
                    return new MemberSlot(type, name, null, MemberSlotKind.EnumValue, true, type, guard);
                }

                lookupType = type;
            }
            else
            {
                lookupType = explicitType ?? target.GetType();
            }

            var member = FindMember(lookupType, name, isStatic);
            if (member == null)
            {
                return new MemberSlot(targetObject, name, null, CarrierKind(targetObject), isStatic, null, guard);
            }

            return new MemberSlot(targetObject, name, member, KindOf(member), isStatic, null, guard);
        }

        /// <summary>
        /// Finds the member with the given name on the given type
        /// </summary>
        /// <remarks>
        /// Takes the first match. With overloads or shadowing that is not deterministic - kept
        /// that way on purpose: this is the behaviour every script has been written against, and
        /// changing it here would change the meaning of existing scripts.
        /// </remarks>
        /// <param name="type">the type on which to look up the name</param>
        /// <param name="memberName">the name to look up</param>
        /// <param name="isStatic">indicates whether to look up static members</param>
        /// <returns>the member that was found, or null</returns>
        public static MemberInfo FindMember(Type type, string memberName, bool isStatic)
        {
            return (from m in type.GetMembers(BindingFlags.Public |
                                              (isStatic ? BindingFlags.Static : BindingFlags.Instance))
                where m.Name == memberName
                select m).FirstOrDefault();
        }

        /// <summary>
        /// Gets the carrier that serves names on the given object, when no member was found
        /// </summary>
        /// <param name="targetObject">the object that is being accessed</param>
        /// <returns>the kind of carrier the object is, or None</returns>
        private static MemberSlotKind CarrierKind(object targetObject)
        {
            if (targetObject is IMemberSource)
            {
                return MemberSlotKind.MemberSource;
            }

            if (targetObject is IDictionary<string, object>)
            {
                return MemberSlotKind.Dictionary;
            }

            if (targetObject is IBasicKeyValueProvider)
            {
                return MemberSlotKind.KeyValueProvider;
            }

            return MemberSlotKind.None;
        }

        /// <summary>
        /// Gets the slot-kind for a member that was found
        /// </summary>
        /// <param name="member">the member that was found</param>
        /// <returns>the kind of the given member</returns>
        private static MemberSlotKind KindOf(MemberInfo member)
        {
            if (member is PropertyInfo)
            {
                return MemberSlotKind.Property;
            }

            if (member is FieldInfo)
            {
                return MemberSlotKind.Field;
            }

            if (member is EventInfo)
            {
                return MemberSlotKind.Event;
            }

            return MemberSlotKind.Unsupported;
        }
    }
}
