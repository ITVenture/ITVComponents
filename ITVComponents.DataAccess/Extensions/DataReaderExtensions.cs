using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;

namespace ITVComponents.DataAccess.Extensions
{
    public static class DataReaderExtensions
    {
        /// <summary>
        /// Creates an expandoObject containing all information provided by the reader
        /// </summary>
        /// <param name="reader">the open sqlreader</param>
        /// <returns>the expandoObject of the current result</returns>
        public static ExpandoObject ToExpandoObject(this IDataReader reader, bool extendWithUppercase)
        {
            ExpandoObject eob = new ExpandoObject();
            IDictionary<string,object> eoc = eob;
            Copy2Collection(reader, eoc, extendWithUppercase);

            return eob;
        }

        /// <summary>
        /// Returns all results of the given reader in an enumerable
        /// </summary>
        /// <param name="reader">the reader containing data</param>
        /// <returns>a list of expandoObjects representing the available data</returns>
        public static IEnumerable<ExpandoObject> ToExpandoObjects(this IDataReader reader, bool extendWithUppercase)
        {
            try
            {
                while (reader.Read())
                {
                    yield return reader.ToExpandoObject(extendWithUppercase);
                }
            }
            finally
            {
                reader.Dispose();
            }
        }

        /// <summary>
        /// Returns all results of the given reader in an enumerable
        /// </summary>
        /// <param name="reader">the reader containing data</param>
        /// <returns>a list of expandoObjects representing the available data</returns>
        public static IEnumerable<IDictionary<string,object>> ToDictionaries(this IDataReader reader, bool caseInsensitive)
        {
            try
            {
                while (reader.Read())
                {
                    yield return reader.ToDictionary(caseInsensitive);
                }
            }
            finally
            {
                reader.Dispose();
            }
        }

        /// <summary>
        /// Creates an expandoObject containing all information provided by the reader
        /// </summary>
        /// <param name="reader">the open sqlreader</param>
        /// <param name="caseInsensitive">indicates whether to create a case-insensitive dictionary</param>
        /// <returns>the expandoObject of the current result</returns>
        public static IDictionary<string, object> ToDictionary(this IDataReader reader, bool caseInsensitive = false)
        {
            Dictionary<string, object> retVal =
                new Dictionary<string, object>(caseInsensitive
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.CurrentCulture);
            Copy2Collection(reader, retVal, false);
            return retVal;
        }

        /// <summary>
        /// Copies the current content of the reader to a dictionary
        /// </summary>
        /// <param name="reader">the reader to copy</param>
        /// <param name="target">the target dictionary</param>
        /// <param name="extendWithUppercase">indicates whether to add upper-case data</param>
        private static void Copy2Collection(IDataReader reader, IDictionary<string, object> target, bool extendWithUppercase)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                object obj = reader.GetValue(i);
                if (obj is DBNull)
                {
                    obj = null;
                }
                /*else if (obj is DateTime)
                {
                    obj = DateTime.SpecifyKind((DateTime) obj, DateTimeKind.Local).ToUniversalTime();
                }*/

                string name = reader.GetName(i);
                target.Add(name, obj);
                if (extendWithUppercase && name.ToUpper() != name)
                {
                    target.Add(name.ToUpper(), obj);
                }
            }
        }
    }
}
