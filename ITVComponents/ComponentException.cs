//-----------------------------------------------------------------------
// <copyright file="ComponentException.cs" company="IT-Venture GmbH">
//     2009 by IT-Venture GmbH
// </copyright>
//-----------------------------------------------------------------------
namespace ITVComponents
{
    using System;

    /// <summary>
    /// Occurs when a IT-Venture Component produces an error
    /// </summary>
    [Serializable]
    public class ComponentException : Exception
    {
        /// <summary>
        /// indicates whether the current Exception is critical or not
        /// </summary>
        private bool critical;

        /// <summary>
        /// Initializes a new instance of the ComponentException class
        /// </summary>
        /// <param name="message">the Errormessage of this exception</param>
        /// <param name="critical">indicates whether the current exception is critical or not</param>
        public ComponentException(string message, bool critical)
            : base(message)
        {
            this.critical = critical;
        }

        /// <summary>
        /// Initializes a new instance of the ComponentException class
        /// </summary>
        /// <param name="message">the Errormessage of this exception</param>
        public ComponentException(string message)
            : this(message, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ComponentException class
        /// </summary>
        /// <param name="message">the Errormessage of this exception</param>
        /// <param name="innerException">the inner exception in this exception</param>
        public ComponentException(string message, Exception innerException)
            : this(message, false, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ComponentException class
        /// </summary>
        /// <param name="message">the Errormessage of this exception</param>
        /// <param name="critical">indicates whether the current exception is critical or not</param>
        /// <param name="innerException">the inner exception in this exception</param>
        public ComponentException(string message, bool critical, Exception innerException)
            : base(message, innerException)
        {
            this.critical = critical;
        }

        /// <summary>
        /// Initializes a new instance of the ComponentException class
        /// </summary>
        /// <param name="info">the serialization info required to deserialize the object</param>
        /// <param name="context">the serialization context</param>
        public ComponentException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
            this.critical = info.GetBoolean("critical");
        }

        /// <summary>
        /// Prevents a default instance of the ComponentException class from being created
        /// </summary>
        private ComponentException()
        {
        }

        /// <summary>
        /// Gets a value indicating whether the current Exception is critical or not
        /// </summary>
        public bool Critical
        {
            get
            {
                return this.critical;
            }
        }

        /// <summary>
        /// Serializes the object
        /// </summary>
        /// <param name="info">the serialization info required to serialize the object</param>
        /// <param name="context">the serialization context</param>
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("critical", this.critical);
        }
    }
}
