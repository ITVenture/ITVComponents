using System;
using System.Reflection;

namespace ITVComponents.MemberAccess
{
    /// <summary>
    /// Provides the guard that is used when a caller does not bring one of its own.
    /// </summary>
    public static class MemberAccessGuard
    {
        /// <summary>
        /// A guard that denies nothing. This is the default: a caller that has no security-model
        /// gets the plain reflection-behaviour, not a silently restricted one.
        /// </summary>
        public static IMemberAccessGuard AllowAll { get; } = new AllowAllGuard();

        private sealed class AllowAllGuard : IMemberAccessGuard
        {
            public bool IsDenied(PropertyInfo property, object target, bool forWrite, bool isStatic)
            {
                return false;
            }

            public bool IsDenied(FieldInfo field, object target, bool forWrite, bool isStatic)
            {
                return false;
            }

            public bool IsDenied(Type type)
            {
                return false;
            }
        }
    }
}
