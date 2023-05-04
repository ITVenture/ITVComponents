using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DIIntegration
{
    public abstract class DbContextUserProvider<T>:ICurrentUserProvider<T>
    where T:DbContext
    {
        public abstract string GetUserName(T dbContext);

        public string GetUserName(DbContext dbContext)
        {
            if (dbContext is not T ctx)
            {
                throw new InvalidOperationException($"Context needs to be of Type {typeof(T).FullName}");
            }

            return GetUserName(ctx);
        }
    }
}
