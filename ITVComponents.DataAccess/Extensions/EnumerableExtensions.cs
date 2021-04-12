using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataAccess.Extensions
{
    /// <summary>
    /// Provides Extension methods for Enumerables
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Loops through the sequence and executes the given Method on each item of it
        /// </summary>
        /// <typeparam name="T">desired ItemType</typeparam>
        /// <param name="sequence">the sequence through which is looped</param>
        /// <param name="itemMethod">the method to apply on every item</param>
        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> itemMethod)
        {
            foreach (T item in sequence)
            {
                itemMethod(item);
            }
        }

        /// <summary>
        /// Loops through the sequence and executes the given Method on each item of it.
        /// </summary>
        /// <typeparam name="T">desired ItemType</typeparam>
        /// <param name="sequence">the sequence through which is looped</param>
        /// <param name="itemMethod">the method to apply on every item</param>
        public static IEnumerable<T> OnEach<T>(this IEnumerable<T> sequence, Action<T> itemMethod)
        {
            foreach (T item in sequence)
            {
                itemMethod(item);
                yield return item;
            }
        }
    }
}
