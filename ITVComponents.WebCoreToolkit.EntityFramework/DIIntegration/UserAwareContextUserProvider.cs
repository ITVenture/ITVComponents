using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.Plugins;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration
{
    public class UserAwareContextUserProvider<T>:DbContextUserProvider<T>
    where T: DbContext, IUserAwareContext
    {
        /// <summary>
        /// Gets the current user-name that is using the given Db-Context
        /// </summary>
        /// <param name="dbContext">the db-context that is currently being used to save some data</param>
        /// <returns>the user-name that will be protocolled as creator or modifier for changed entities</returns>
        public override string GetUserName(T dbContext)
        {
            return dbContext.CurrentUserName;
        }
    }
}
