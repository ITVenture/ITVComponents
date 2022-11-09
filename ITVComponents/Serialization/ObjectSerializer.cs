using System;
using System.IO;
using System.Runtime.Serialization;

namespace ITVComponents.Serialization
{
    /// <summary>
    /// Provides object serialization capabilities to any object
    /// </summary>
    [Obsolete("Not supported anymore!", true)]
    public class ObjectSerializer<TFormatterType> where TFormatterType : IFormatter, new()
    {
        /// <summary>
        /// The Formatter used to store the objects in a file-stream
        /// </summary>
        private TFormatterType formatter;

        /// <summary>
        /// Initializes a new instance of the ObjectSerializer class
        /// </summary>
        public ObjectSerializer()
        {
            formatter = new TFormatterType();
        }

        /// <summary>
        /// Loads an ObjectSerializer instance from a file
        /// </summary>
        /// <param name="sourceFileName">the sourceFile name in which this serializer instance is stored</param>
        /// <returns>the serializer instance that was stored in the specified file</returns>
        public RuntimeInformation Load(string sourceFileName)
        {
            RuntimeInformation retVal;
            using (FileStream fst = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read))
            {
                retVal = formatter.Deserialize(fst) as RuntimeInformation;
            }

            File.Delete(sourceFileName);
            return retVal;
        }

        /// <summary>
        /// Saves this object into a fileStream
        /// </summary>
        /// <param name="targetFileName">the fileName in which this object is stored</param>
        /// <param name="targetObject">the generated runtime information used to save the file</param>
        public void Save(string targetFileName, RuntimeInformation targetObject)
        {
            using (FileStream fst = new FileStream(targetFileName, FileMode.Create, FileAccess.Write))
            {
                try
                {
                    formatter.Serialize(fst, targetObject);
                }
                finally
                {
                    fst.Flush();
                }
            }
        }
    }
}