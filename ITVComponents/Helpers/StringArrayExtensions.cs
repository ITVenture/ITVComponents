using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Helpers
{
    /// <summary>
    /// String array extensions
    /// </summary>
    public static class StringArrayExtensions
    {
        /// <summary>
        /// Gets the first index that matches the searched value with the given stringcomparison mode
        /// </summary>
        /// <param name="targetArray">the target array to search for the value</param>
        /// <param name="value">the value to search</param>
        /// <param name="mode">the comparison mode to apply on the items</param>
        /// <returns>the first index where the provided search-value appears</returns>
        public static int IndexOf(this string[] targetArray, string value, StringComparison mode)
        {
            return (from t in targetArray.Select((v, i) => new {Index = i, Value = v})
             where value.Equals(t.Value, mode)
             select t.Index).DefaultIfEmpty(-1).FirstOrDefault();
        }
    }
}
