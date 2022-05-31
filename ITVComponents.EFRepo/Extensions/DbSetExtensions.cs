using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.Extensions
{
    public static class DbSetExtensions
    {
        /// <summary>
        /// Finds the first record that is either buffered or stored in the database, that fits the given criteria
        /// </summary>
        /// <typeparam name="T">the entity type</typeparam>
        /// <param name="dbset">the db-set on which to perform the search</param>
        /// <param name="condition">the condition to check on the entity</param>
        /// <returns>the first fitting value or null</returns>
        public static T LocalFirstOrDefault<T>(this DbSet<T> dbset, Expression<Func<T, bool>> condition) where T : class
        {
            return dbset.Local.FirstOrDefault(condition.Compile(true)) ?? dbset.FirstOrDefault(condition);
        }

        /// <summary>
        /// Finds the first record that is either buffered or stored in the database, that fits the given criteria
        /// </summary>
        /// <typeparam name="T">the entity type</typeparam>
        /// <param name="dbset">the db-set on which to perform the search</param>
        /// <param name="condition">the condition to check on the entity</param>
        /// <returns>the first fitting value</returns>
        public static T LocalFirst<T>(this DbSet<T> dbset, Expression<Func<T, bool>> condition) where T : class
        {
            return dbset.Local.FirstOrDefault(condition.Compile(true)) ?? dbset.First(condition);
        }
    }
}
