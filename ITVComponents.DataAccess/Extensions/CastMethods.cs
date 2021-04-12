using System;
using ITVComponents.TypeConversion;

namespace ITVComponents.DataAccess.Extensions
{
    public static class CastMethods
    {
        /// <summary>
        /// Enables an object to cast to any Type
        /// </summary>
        /// <typeparam name="T">the target type</typeparam>
        /// <param name="obj">the object that converts</param>
        /// <returns>the converted value</returns>
        public static T Cast<T>(this object obj)
        {
            bool success;
            return CastPrivate<T>(obj, out success);
        }

        /// <summary>
        /// Converts a value to a demanded value or to the defaultvalue provided
        /// </summary>
        /// <typeparam name="T">the type in which to cast the provided object</typeparam>
        /// <param name="obj">the object that is being converted</param>
        /// <param name="replacementValue">the value to return in case that the passed object can not be converted into the desired type</param>
        /// <returns>the converted value</returns>
        public static T TryCast<T>(this object obj, T replacementValue)
        {
            try
            {
                bool success;
                T retVal = CastPrivate<T>(obj, out success);
                if (success)
                {
                    return retVal;
                }
            }
            catch
            {
            }

            return replacementValue;
        }

        /// <summary>
        /// Gets the first item's specific column of a recordset or returns the provided default value
        /// </summary>
        /// <typeparam name="T">the type that is expected</typeparam>
        /// <param name="items">the array from which to get the first item</param>
        /// <param name="columnName">the columnname to retreive from the first item</param>
        /// <param name="defaultValue">the default value in case that the array does not contain any items</param>
        /// <returns>the demanded value or the default value</returns>
        public static T FirstValueOrDefault<T>(this dynamic[] items, string columnName, T defaultValue)
        {
            T retVal = defaultValue;
            if (items.Length != 0)
            {
                DynamicResult r = items[0];
                retVal = r[columnName];
            }

            return retVal;
        }

        /// <summary>
        /// Converts an object to another type
        /// </summary>
        /// <typeparam name="T">the type to cast to</typeparam>
        /// <param name="obj">the object to convert</param>
        /// <param name="success">indicates whether the conversion was successful</param>
        /// <returns>the result of the conversion</returns>
        private static T CastPrivate<T>(object obj, out bool success)
        {
            T retVal = default(T);
            success = false;
            if (obj != null)
            {
                try
                {
                    retVal = (T)obj;
                    success = true;
                }
                catch
                {
                }

                if (!success)
                {
                    if (!typeof(T).IsEnum)
                    {
                        retVal = (T)TypeConverter.Convert(obj, typeof(T));
                    }
                    else
                    {
                        retVal = (T)Enum.Parse(typeof(T), obj.ToString());
                    }

                    success = true;
                }
            }

            return retVal;
        }
    }
}
