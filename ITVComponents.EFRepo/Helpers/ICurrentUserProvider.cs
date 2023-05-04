using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.Helpers
{
    public interface ICurrentUserProvider
    {
        /// <summary>
        /// Gets the current user-name that is using the given Db-Context
        /// </summary>
        /// <param name="dbContext">the db-context that is currently being used to save some data</param>
        /// <returns>the user-name that will be protocolled as creator or modifier for changed entities</returns>
        public string GetUserName(DbContext dbContext);
    }

    public interface ICurrentUserProvider<T>:ICurrentUserProvider
        where T : DbContext
    {
        /// <summary>
        /// Gets the current user-name that is using the given Db-Context
        /// </summary>
        /// <param name="dbContext">the db-context that is currently being used to save some data</param>
        /// <returns>the user-name that will be protocolled as creator or modifier for changed entities</returns>
        public string GetUserName(T dbContext);
    }
}
