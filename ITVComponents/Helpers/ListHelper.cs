using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.Helpers
{
    /// <summary>
    /// Tool class supporting shortcuts with lists
    /// </summary>
    public static class ListHelper
    {
        /// <summary>
        /// Creates a new List of a specific Type.
        /// For Using with anonymous Types call "var foo = CreateDynamicLst(new{ field1="Value1", field2=2 });"
        /// </summary>
        /// <typeparam name="T">the Type of the requested list</typeparam>
        /// <param name="firstItem">the first item to add to the list</param>
        /// <returns>a list of the demanded type</returns>
        public static List<T> CreateDynamicList<T>(T firstItem)
        {
            return new List<T> {firstItem};
        }

        public static void AddIfMissing<T>(this IList<T> list, T item, bool omitDefault = false)
        {
            var eq = EqualityComparer<T>.Default;
            var isDefault = eq.Equals(item, default(T));
            if (!isDefault || !omitDefault)
            {
                if (list.All(n =>
                    {
                        var nIsDefault = eq.Equals(n, default(T));
                        var isEqual = (isDefault && nIsDefault || n.Equals(item));
                        var add = !isEqual;
                        return add;
                    }))
                {
                    list.Add(item);
                }
            }
        }
    }
}
