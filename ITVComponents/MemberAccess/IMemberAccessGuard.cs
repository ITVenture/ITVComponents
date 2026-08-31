using System;
using System.Reflection;

namespace ITVComponents.MemberAccess
{
    /// <summary>
    /// Decides whether a member may be read or written. The default
    /// (<see cref="MemberAccessGuard.AllowAll"/>) allows everything; the script-interpreter
    /// implements this over its ScriptingPolicy, so that a path in a form and the same path in
    /// an expression answer to the same rules.
    /// </summary>
    public interface IMemberAccessGuard
    {
        /// <summary>
        /// Gets a value indicating whether the access to the given property is denied
        /// </summary>
        /// <param name="property">the property that is about to be accessed</param>
        /// <param name="target">the object the property is read from or written to; null when static</param>
        /// <param name="forWrite">indicates whether the property is about to be written</param>
        /// <param name="isStatic">indicates whether the access is a static one</param>
        /// <returns>a value indicating whether the access is denied</returns>
        bool IsDenied(PropertyInfo property, object target, bool forWrite, bool isStatic);

        /// <summary>
        /// Gets a value indicating whether the access to the given field is denied
        /// </summary>
        /// <param name="field">the field that is about to be accessed</param>
        /// <param name="target">the object the field is read from or written to; null when static</param>
        /// <param name="forWrite">indicates whether the field is about to be written</param>
        /// <param name="isStatic">indicates whether the access is a static one</param>
        /// <returns>a value indicating whether the access is denied</returns>
        bool IsDenied(FieldInfo field, object target, bool forWrite, bool isStatic);

        /// <summary>
        /// Gets a value indicating whether the direct access to the given type is denied. Asked
        /// when a named value of an enum-type is read.
        /// </summary>
        /// <param name="type">the type that is about to be accessed directly</param>
        /// <returns>a value indicating whether the access is denied</returns>
        bool IsDenied(Type type);
    }
}
