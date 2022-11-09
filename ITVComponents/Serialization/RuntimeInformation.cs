using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;

namespace ITVComponents.Serialization
{
    [Serializable, Obsolete("Not supported anymore", true)]
    public class RuntimeInformation : IEnumerable, ISerializable
    {
        /// <summary>
        /// a list of registered objects that must be serialized whithin this serializer object
        /// </summary>
        private Dictionary<string, object> serializationObjects =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public RuntimeInformation()
        {

        }

        public RuntimeInformation(SerializationInfo info, StreamingContext context)
        {
            var tmp = (Dictionary<string, object>) info.GetValue("serializedObjects", typeof(Dictionary<string, object>));
            foreach (var iitem in tmp)
            {
                serializationObjects.Add(iitem.Key, iitem.Value);
            }
        }

        /// <summary>
        /// Gets or sets the value for a specific key
        /// </summary>
        /// <param name="objectName">the object to store in the target file</param>
        /// <returns>the value of the specified object or null if the key was not found</returns>
        public object this[string objectName]
        {
            get
            {
                object retVal = null;
                if (serializationObjects.ContainsKey(objectName))
                {
                    retVal = serializationObjects[objectName];
                }

                return retVal;
            }

            set
            {
                if (serializationObjects.ContainsKey(objectName))
                {
                    serializationObjects[objectName] = value;
                }
                else
                {
                    serializationObjects.Add(objectName, value);
                }
            }
        }

        /// <summary>
        /// Adds an object to this serializer instance (provides the possibility to use the Object-Initializer -> new ObjectSerializer{{"objectA",objectA},...};
        /// </summary>
        /// <param name="objectName">the name of the Object</param>
        /// <param name="value">the instance that is identified with the given name</param>
        public void Add(string objectName, object value)
        {
            serializationObjects.Add(objectName, value);
            if (value != null)
            {
                if (value.GetType().ToString().Contains("ThreadQueue"))
                {
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the objects of this ObjectSerializer
        /// </summary>
        public IEnumerator GetEnumerator()
        {
            return serializationObjects.GetEnumerator();
        }

        /// <summary>Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with the data needed to serialize the target object.</summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> to populate with data.</param>
        /// <param name="context">The destination (see <see cref="T:System.Runtime.Serialization.StreamingContext" />) for this serialization.</param>
        /// <exception cref="T:System.Security.SecurityException">The caller does not have the required permission.</exception>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("serializedObjects", serializationObjects);
        }
    }
}