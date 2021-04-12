using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ITVComponents.DataAccess.Extensions;
namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    [Serializable]
    public class SerializedException
    {
        /// <summary>
        /// Holds all additional information that was collected from the original exception
        /// </summary>
        private Dictionary<string,string> collectedInformation = new Dictionary<string, string>();

        /// <summary>
        /// Initializes a new instance of the SerializedException class
        /// </summary>
        /// <param name="message">the error message describing this exception</param>
        /// <param name="innerExceptions">exceptions that have caused this exception</param>
        public SerializedException(string message, IEnumerable<SerializedException> innerExceptions)
        {
            this.Message = message;
            if (innerExceptions != null)
            {
                this.InnerException = innerExceptions.ToArray();
            }
        }

        /// <summary>
        /// Prevents a default instance of the SerializedException class from being created
        /// </summary>
        public SerializedException()
        {
        }

        /// <summary>
        /// Casts an exception implicitly into a serialized exception object which can be transferred safely over application fences
        /// </summary>
        /// <param name="exception">the exception that was thrown by a component</param>
        /// <returns>a Serializable object containing all relevant information about the exception</returns>
        public static implicit operator SerializedException(Exception exception)
        {
            if (exception != null)
            {
                SerializedException retVal = new SerializedException
                                                 {
                                                     ExceptionType = exception.GetType().FullName,
                                                     InnerException =
                                                         exception.InnerException != null
                                                             ? new SerializedException[] {exception.InnerException}
                                                             : null,
                                                     Message = exception.Message,
                                                     StackTrace = exception.StackTrace,
                                                     HelpLink = exception.HelpLink,
                                                     Source = exception.Source
                                                 };
                retVal.CollectFurtherInformation(exception);
                return retVal;
            }

            return null;
        }

        /// <summary>
        /// Gets the information that was collected from the original exception
        /// </summary>
        /// <param name="informationKey">the name of the demanded original-Property</param>
        /// <returns>the value of the demanded property</returns>
        [IgnoreDataMember]
        public object this[string informationKey]
        {
            get
            {
                object retVal = null;
                if (collectedInformation.ContainsKey(informationKey))
                {
                    retVal = collectedInformation[informationKey];
                }

                return retVal;
            }
        }

        /// <summary>
        /// Gets an array containing all additional information that was collected for this serialized exception
        /// </summary>
        [IgnoreDataMember]
        public string[] InformationKeys { get { return collectedInformation.Keys.ToArray(); } }

        /// <summary>
        /// Gets or sets the Collected information for this serialized exception
        /// </summary>
        public KeyValuePair<string, string>[] CollectedInformation
        {
            get { return collectedInformation.ToArray(); }
            set { value.ForEach(n => collectedInformation[n.Key] = n.Value); }
        }

        /// <summary>
        /// Gets the Source of the Exception that was thrown
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets the HelpLink of the exception that was thrown
        /// </summary>
        public string HelpLink { get; set; }

        /// <summary>
        /// Gets the Exception type of the thrown exception
        /// </summary>
        public string ExceptionType { get; set; }

        /// <summary>
        /// Gets the Message of the thrown exception
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets the inner exception of this exception object
        /// </summary>
        public SerializedException[] InnerException { get; set; }

        /// <summary>
        /// Gets the StackTrace of the thrown exception
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// Gibt einen <see cref="T:System.String"/> zurück, der das aktuelle <see cref="T:System.Object"/> darstellt.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.String"/>, der das aktuelle <see cref="T:System.Object"/> darstellt.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return $@"{ExceptionType}: {Message}
{StackTrace}
Exception-Data:
{string.Join(@"
", from t in collectedInformation select $"{t.Key}: {t.Value}")}
{new string('-', 80)}
{((InnerException != null) ? string.Join($@"
{new string('-', 80)}", InnerException?.Select(n => n.ToString()) ?? new string[] {""}) : "")}";
        }

        /// <summary>
        /// Checks for specific error types
        /// </summary>
        /// <param name="exceptionTypes">the desired types</param>
        /// <returns>a value indicating whether the specified type was found</returns>
        public bool ContainsType(params string[] exceptionTypes)
        {
            bool retVal =
                exceptionTypes.Select(
                    t => ExceptionType != null && ExceptionType.Contains(t)).Any(n=>n);
            if (!retVal && InnerException != null)
            {
                retVal |= InnerException.Any(n => n.ContainsType(exceptionTypes));
            }

            return retVal;
        }

        /// <summary>
        /// Collects further information for the exception that is serialized by this object
        /// </summary>
        /// <param name="ex">the original exception</param>
        private void CollectFurtherInformation(Exception ex)
        {
            string[] tps = new[] { "Message", "HelpLink", "InnerException", "Source", "StackTrace" };
            Type[] safeTypes = new[] { typeof(string), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(bool), typeof(SerializedException) };
            Type t = ex?.GetType();
            if (t != null)
            {
                var props = t.GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public |
                                BindingFlags.GetProperty);
                var values = (from p in props where !tps.Contains(p.Name) && p.GetIndexParameters().Length==0 select p).Select(n => new {Name = n.Name, Type = n.PropertyType, Value=n.GetValue(ex)});
                (from v in values where v.Value != null && (Attribute.IsDefined(v.Type, typeof(DataContractAttribute)) || safeTypes.Contains(v.Type)) select v).ToArray().ForEach(
                    n =>
                    {
                        collectedInformation.Add(n.Name, n.Value?.ToString());                         
                    });
            }
        }
    }
}
