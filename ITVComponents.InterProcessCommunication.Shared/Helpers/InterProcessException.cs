using System;
using System.Runtime.Serialization;
using ITVComponents.Helpers;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    public class InterProcessException : Exception, ExceptionHelper.IAutoOutline
    {
        /// <summary>
        /// The ServerException that caused this InterProcessException
        /// </summary>
        private SerializedException serverException;

        /// <summary>
        /// Initializes a new instance of the InterProcessException class
        /// </summary>
        /// <param name="serverException">the server Exception that was generated from by an interprocess call</param>
        public InterProcessException(SerializedException serverException)
        {
            this.serverException = serverException;
        }

        /// <summary>
        /// Initializes a new instance of the InterProcessException class
        /// </summary>
        /// <param name="message">the errormessage</param>
        /// <param name="inner">the exception that caused this error</param>
        /// <param name="serverException">the server Exception that was generated from by an interprocess call</param>
        public InterProcessException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the InterProcessException class
        /// </summary>
        /// <param name="message">the errormessage</param>
        /// <param name="serverException">the server Exception that was generated from by an interprocess call</param>
        public InterProcessException(string message, SerializedException serverException)
            : base(message)
        {
            this.serverException = serverException;
        }

        #region Overrides of Exception

        public override string Message { get { return ToString(); } }

        #endregion

        /// <summary>
        /// Gets the Server Exception that caused this exception
        /// </summary>
        public SerializedException ServerException
        {
            get { return serverException; }
        }

        /// <summary>
        /// Outlines the content of the current exception
        /// </summary>
        /// <returns></returns>
        public string Outline()
        {
            return ToString();
        }

        public override string ToString()
        {
            return string.Format(@"{0}
{1}
{2}
innerException:
{3}
{2}
server Exception: 
{4}", base.Message, StackTrace, new string('-', 80), InnerException?.OutlineException(), serverException);
        }
    }
}