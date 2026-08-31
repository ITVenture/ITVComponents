using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using ITVComponents.MemberAccess;
using ITVComponents.Scripting.CScript.Security.Restrictions;

namespace ITVComponents.Scripting.CScript.Security
{
    /// <summary>
    /// Answers the member-access questions of <see cref="MemberAccessor"/> with the rules of a
    /// <see cref="ScriptingPolicy"/>.
    /// </summary>
    /// <remarks>
    /// This is what keeps the two access-paths on one security-level: a path that is refused as an
    /// expression must be refused as a form-field, too.
    /// </remarks>
    public sealed class ScriptingPolicyMemberAccessGuard : IMemberAccessGuard
    {
        /// <summary>
        /// the guards that have been created so far. Member-access is a hot path; a policy is
        /// usually one of very few instances, so its guard is created once and kept as long as
        /// the policy lives.
        /// </summary>
        private static readonly ConditionalWeakTable<ScriptingPolicy, ScriptingPolicyMemberAccessGuard> Guards = new();

        /// <summary>
        /// the policy that decides
        /// </summary>
        private readonly ScriptingPolicy policy;

        /// <summary>
        /// Initializes a new instance of the ScriptingPolicyMemberAccessGuard class
        /// </summary>
        /// <param name="policy">the policy that decides</param>
        private ScriptingPolicyMemberAccessGuard(ScriptingPolicy policy)
        {
            this.policy = policy;
        }

        /// <summary>
        /// Gets the guard for the given policy
        /// </summary>
        /// <param name="policy">the policy for which to get a guard; null yields the default-policy</param>
        /// <returns>the guard that applies the rules of the given policy</returns>
        public static IMemberAccessGuard For(ScriptingPolicy policy)
        {
            var target = policy ?? ScriptingPolicy.Default;
            return Guards.GetValue(target, p => new ScriptingPolicyMemberAccessGuard(p));
        }

        /// <summary>
        /// Gets a value indicating whether the access to the given property is denied
        /// </summary>
        /// <param name="property">the property that is about to be accessed</param>
        /// <param name="target">the object the property is read from or written to; null when static</param>
        /// <param name="forWrite">indicates whether the property is about to be written</param>
        /// <param name="isStatic">indicates whether the access is a static one</param>
        /// <returns>a value indicating whether the access is denied</returns>
        public bool IsDenied(PropertyInfo property, object target, bool forWrite, bool isStatic)
        {
            return policy.IsDenied(property, target,
                forWrite ? PropertyAccessMode.Write : PropertyAccessMode.Read, isStatic);
        }

        /// <summary>
        /// Gets a value indicating whether the access to the given field is denied
        /// </summary>
        /// <param name="field">the field that is about to be accessed</param>
        /// <param name="target">the object the field is read from or written to; null when static</param>
        /// <param name="forWrite">indicates whether the field is about to be written</param>
        /// <param name="isStatic">indicates whether the access is a static one</param>
        /// <returns>a value indicating whether the access is denied</returns>
        public bool IsDenied(FieldInfo field, object target, bool forWrite, bool isStatic)
        {
            return policy.IsDenied(field, target,
                forWrite ? FieldAccessMode.Write : FieldAccessMode.Read, isStatic);
        }

        /// <summary>
        /// Gets a value indicating whether the direct access to the given type is denied
        /// </summary>
        /// <param name="type">the type that is about to be accessed directly</param>
        /// <returns>a value indicating whether the access is denied</returns>
        public bool IsDenied(Type type)
        {
            return policy.IsDenied(type, TypeAccessMode.Direct, policy.PolicyMode);
        }
    }
}
