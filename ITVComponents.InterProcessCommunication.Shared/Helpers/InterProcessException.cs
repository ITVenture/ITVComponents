using System;
using System.Runtime.Serialization;
using ITVComponents.Helpers;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    [Serializable]
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
        /// <param name="serverException">the server Exception that was generated from by an interprocess call</param>
        public InterProcessException(string message, SerializedException serverException)
            : base(message)
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
        /// <param name="info">the serialized object data</param>
        /// <param name="context">the serialization context</param>
        protected InterProcessException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
            this.serverException = (SerializedException)info.GetValue("serverException", typeof (SerializedException));
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
        /// Legt beim Überschreiben in einer abgeleiteten Klasse die <see cref="T:System.Runtime.Serialization.SerializationInfo"/> mit Informationen über die Ausnahme fest.
        /// </summary>
        /// <param name="info">Die <see cref="T:System.Runtime.Serialization.SerializationInfo"/>-Klasse, die die serialisierten Objektdaten für die ausgelöste Ausnahme enthält. </param><param name="context">Der <see cref="T:System.Runtime.Serialization.StreamingContext"/>, der die Kontextinformationen über die Quelle oder das Ziel enthält. </param><exception cref="T:System.ArgumentNullException">Der <paramref name="info"/>-Parameter ist ein NULL-Verweis (Nothing in Visual Basic). </exception><filterpriority>2</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Read="*AllFiles*" PathDiscovery="*AllFiles*"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="SerializationFormatter"/></PermissionSet>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("serverException", serverException);
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