using System;
using System.Collections.Generic;
using System.Reflection;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.MemberAccess
{
    /// <summary>
    /// One resolved name on one target: read it, write it, ask whether it is there.
    /// </summary>
    /// <remarks>
    /// A slot is the plain access, without interpreter-machinery: no locking, no inline-cache,
    /// no disposal. It is cheap to create and not meant to be kept: it holds the target alive.
    /// </remarks>
    public sealed class MemberSlot
    {
        /// <summary>
        /// the guard that decides whether the access may happen
        /// </summary>
        private readonly IMemberAccessGuard guard;

        /// <summary>
        /// Initializes a new instance of the MemberSlot class
        /// </summary>
        /// <param name="target">the object the member is accessed on; null for a static access</param>
        /// <param name="name">the name that was resolved</param>
        /// <param name="member">the member that was found, or null when a carrier serves the name</param>
        /// <param name="kind">what was found for the name</param>
        /// <param name="isStatic">indicates whether the access is a static one</param>
        /// <param name="enumType">the enum-type, when the slot stands for a named enum-value</param>
        /// <param name="guard">the guard that decides whether the access may happen</param>
        internal MemberSlot(object target, string name, MemberInfo member, MemberSlotKind kind, bool isStatic,
            Type enumType, IMemberAccessGuard guard)
        {
            Target = target;
            Name = name;
            Member = member;
            Kind = kind;
            IsStatic = isStatic;
            EnumType = enumType;
            this.guard = guard ?? MemberAccessGuard.AllowAll;
        }

        /// <summary>
        /// Gets the object the member is accessed on. Null for a static access; the enum-type
        /// itself when the slot stands for a named enum-value.
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// Gets the name that was resolved
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the member that was found, or null when a carrier serves the name
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// Gets what was found for the name
        /// </summary>
        public MemberSlotKind Kind { get; }

        /// <summary>
        /// Gets a value indicating whether the access is a static one
        /// </summary>
        public bool IsStatic { get; }

        /// <summary>
        /// Gets the enum-type, when this slot stands for a named enum-value
        /// </summary>
        public Type EnumType { get; }

        /// <summary>
        /// Gets the declared type of the member, or null when it has none that can be named
        /// without reading it (a property without a getter, a name a carrier does not have).
        /// </summary>
        /// <returns>the declared type of the member</returns>
        public Type GetMemberType()
        {
            switch (Kind)
            {
                case MemberSlotKind.Property:
                {
                    var pi = (PropertyInfo)Member;
                    return pi.CanRead ? pi.PropertyType : null;
                }
                case MemberSlotKind.Field:
                    return ((FieldInfo)Member).FieldType;
                case MemberSlotKind.Event:
                    return ((EventInfo)Member).EventHandlerType;
                case MemberSlotKind.EnumValue:
                    return EnumType;
                case MemberSlotKind.Dictionary:
                case MemberSlotKind.KeyValueProvider:
                case MemberSlotKind.MemberSource:
                {
                    if (!Exists())
                    {
                        return null;
                    }

                    TryRead(out var value);
                    return value?.GetType() ?? typeof(object);
                }
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the name is present and readable
        /// </summary>
        /// <returns>a value indicating whether the name can be read</returns>
        public bool Exists()
        {
            switch (Kind)
            {
                case MemberSlotKind.Property:
                {
                    var pi = (PropertyInfo)Member;
                    return !guard.IsDenied(pi, Target, false, IsStatic) && pi.CanRead;
                }
                case MemberSlotKind.Field:
                    return !guard.IsDenied((FieldInfo)Member, Target, false, IsStatic);
                case MemberSlotKind.Event:
                    return true;
                case MemberSlotKind.EnumValue:
                    return !guard.IsDenied(EnumType) && Enum.TryParse(EnumType, Name, out _);
                case MemberSlotKind.Dictionary:
                    return ((IDictionary<string, object>)Target).ContainsKey(Name);
                case MemberSlotKind.KeyValueProvider:
                    return ((IBasicKeyValueProvider)Target).ContainsKey(Name);
                case MemberSlotKind.MemberSource:
                    return ((IMemberSource)Target).ContainsMember(Name);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Tries to read the value of this slot and reports why it did not work
        /// </summary>
        /// <param name="value">the value that was read</param>
        /// <returns>the outcome of the attempt</returns>
        public MemberAccessResult TryRead(out object value)
        {
            value = null;
            switch (Kind)
            {
                case MemberSlotKind.Property:
                {
                    var pi = (PropertyInfo)Member;
                    if (guard.IsDenied(pi, Target, false, IsStatic))
                    {
                        return MemberAccessResult.Denied;
                    }

                    if (!pi.CanRead)
                    {
                        return MemberAccessResult.NotReadable;
                    }

                    value = pi.GetValue(Target, null);
                    return MemberAccessResult.Ok;
                }
                case MemberSlotKind.Field:
                {
                    var fi = (FieldInfo)Member;
                    if (guard.IsDenied(fi, Target, false, IsStatic))
                    {
                        return MemberAccessResult.Denied;
                    }

                    value = fi.GetValue(Target);
                    return MemberAccessResult.Ok;
                }
                case MemberSlotKind.Event:
                    // an event has no value to read; saying so is not an error
                    return MemberAccessResult.Ok;
                case MemberSlotKind.EnumValue:
                {
                    if (guard.IsDenied(EnumType))
                    {
                        return MemberAccessResult.Denied;
                    }

                    // a name that is not a value of the enum yields null, as it always has
                    Enum.TryParse(EnumType, Name, out value);
                    return MemberAccessResult.Ok;
                }
                case MemberSlotKind.Dictionary:
                {
                    var dictionary = (IDictionary<string, object>)Target;
                    if (dictionary.ContainsKey(Name))
                    {
                        value = dictionary[Name];
                    }

                    return MemberAccessResult.Ok;
                }
                case MemberSlotKind.KeyValueProvider:
                {
                    var provider = (IBasicKeyValueProvider)Target;
                    if (provider.ContainsKey(Name))
                    {
                        value = provider[Name];
                    }

                    return MemberAccessResult.Ok;
                }
                case MemberSlotKind.MemberSource:
                {
                    var source = (IMemberSource)Target;
                    if (source.ContainsMember(Name))
                    {
                        value = source.GetMember(Name);
                    }

                    return MemberAccessResult.Ok;
                }
                case MemberSlotKind.Unsupported:
                    return MemberAccessResult.Unsupported;
                default:
                    return MemberAccessResult.NotFound;
            }
        }

        /// <summary>
        /// Tries to write the value of this slot and reports why it did not work
        /// </summary>
        /// <param name="value">the value to write</param>
        /// <returns>the outcome of the attempt</returns>
        public MemberAccessResult TryWrite(object value)
        {
            switch (Kind)
            {
                case MemberSlotKind.Property:
                {
                    var pi = (PropertyInfo)Member;
                    if (!pi.CanWrite)
                    {
                        return MemberAccessResult.NotWritable;
                    }

                    if (guard.IsDenied(pi, Target, true, IsStatic))
                    {
                        return MemberAccessResult.Denied;
                    }

                    pi.SetValue(Target, value, null);
                    return MemberAccessResult.Ok;
                }
                case MemberSlotKind.Field:
                {
                    var fi = (FieldInfo)Member;
                    if (fi.IsLiteral)
                    {
                        return MemberAccessResult.NotWritable;
                    }

                    if (guard.IsDenied(fi, Target, true, IsStatic))
                    {
                        return MemberAccessResult.Denied;
                    }

                    fi.SetValue(Target, value);
                    return MemberAccessResult.Ok;
                }
                case MemberSlotKind.Dictionary:
                    ((IDictionary<string, object>)Target)[Name] = value;
                    return MemberAccessResult.Ok;
                case MemberSlotKind.MemberSource:
                    ((IMemberSource)Target).SetMember(Name, value);
                    return MemberAccessResult.Ok;
                case MemberSlotKind.KeyValueProvider:
                case MemberSlotKind.EnumValue:
                    return MemberAccessResult.NotWritable;
                case MemberSlotKind.Event:
                case MemberSlotKind.Unsupported:
                    return MemberAccessResult.Unsupported;
                default:
                    return MemberAccessResult.NotFound;
            }
        }

        /// <summary>
        /// Reads the value of this slot
        /// </summary>
        /// <remarks>
        /// A member without a getter yields null instead of an error - that is the behaviour of
        /// the script-interpreter, and the two must not disagree about what a.b means.
        /// </remarks>
        /// <returns>the value of this slot</returns>
        public object Read()
        {
            var result = TryRead(out var value);
            if (result == MemberAccessResult.Ok || result == MemberAccessResult.NotReadable)
            {
                return value;
            }

            throw new MemberAccessFailedException(Describe(result, false), result);
        }

        /// <summary>
        /// Writes the value of this slot
        /// </summary>
        /// <param name="value">the value to write</param>
        public void Write(object value)
        {
            var result = TryWrite(value);
            if (result != MemberAccessResult.Ok)
            {
                throw new MemberAccessFailedException(Describe(result, true), result);
            }
        }

        /// <summary>
        /// Builds the message for a failed access. The type is named because the caller usually
        /// can not see it: it is only known at runtime.
        /// </summary>
        /// <param name="result">the outcome that is to be described</param>
        /// <param name="forWrite">indicates whether the failed access was a write</param>
        /// <returns>a message describing the failed access</returns>
        internal string Describe(MemberAccessResult result, bool forWrite)
        {
            var verb = forWrite ? "write" : "read";
            string owner;
            if (Target is Type staticTarget)
            {
                owner = staticTarget.FullName;
            }
            else if (Target != null)
            {
                owner = Target.GetType().FullName;
            }
            else
            {
                owner = Member?.DeclaringType?.FullName ?? "<unknown>";
            }

            switch (result)
            {
                case MemberAccessResult.NotFound:
                    return $"Unable to {verb} '{Name}': not declared on {owner}.";
                case MemberAccessResult.Denied:
                    return $"Unable to {verb} '{Name}' on {owner}: access is denied.";
                case MemberAccessResult.NotReadable:
                    return $"Unable to read '{Name}' on {owner}: the member has no getter.";
                case MemberAccessResult.NotWritable:
                    return $"Unable to write '{Name}' on {owner}: the member can not be written.";
                default:
                    return $"Unable to {verb} '{Name}' on {owner}: {Kind} is not supported here.";
            }
        }
    }
}
