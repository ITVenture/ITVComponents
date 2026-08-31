using System;

namespace ITVComponents.MemberAccess
{
    /// <summary>
    /// Occurs when a member-access fails and the caller has not asked for the reason.
    /// </summary>
    /// <remarks>
    /// Deliberately not named MemberAccessException: that name is taken by
    /// <see cref="System.MemberAccessException"/>, and a second one would be ambiguous in every
    /// file that has both namespaces in scope.
    /// </remarks>
    public class MemberAccessFailedException : ComponentException
    {
        /// <summary>
        /// Initializes a new instance of the MemberAccessFailedException class
        /// </summary>
        /// <param name="message">the error-message of this exception</param>
        public MemberAccessFailedException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the MemberAccessFailedException class
        /// </summary>
        /// <param name="message">the error-message of this exception</param>
        /// <param name="result">the reason the access failed</param>
        public MemberAccessFailedException(string message, MemberAccessResult result) : base(message)
        {
            Result = result;
        }

        /// <summary>
        /// Initializes a new instance of the MemberAccessFailedException class
        /// </summary>
        /// <param name="message">the error-message of this exception</param>
        /// <param name="innerException">the exception that caused this one</param>
        public MemberAccessFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Gets the reason the access failed
        /// </summary>
        public MemberAccessResult Result { get; } = MemberAccessResult.NotFound;
    }
}
