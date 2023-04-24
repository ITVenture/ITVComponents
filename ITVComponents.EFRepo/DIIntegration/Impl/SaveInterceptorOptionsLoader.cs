using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Interceptors;
using ITVComponents.EFRepo.Options;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DIIntegration.Impl
{
    public class SaveInterceptorOptionsLoader<TContext>: ContextOptionsLoader<TContext> where TContext : DbContext
    {
        private readonly string userName;
        private readonly bool useUtc;

        public SaveInterceptorOptionsLoader(string userName, bool useUtc)
        {
            this.userName = userName;
            this.useUtc = useUtc;
        }
        protected override void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> builder)
        {
            builder.AddInterceptors(new ModCreateInterceptor(userName,useUtc));
        }
    }
}
