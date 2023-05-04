using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Interceptors;
using ITVComponents.EFRepo.Options;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DIIntegration.Impl
{
    public class SaveInterceptorOptionsLoader<TContext>: ContextOptionsLoader<TContext> where TContext : DbContext
    {
        private readonly ICurrentUserProvider userProvider;
        private readonly bool useUtc;

        public SaveInterceptorOptionsLoader(ContextOptionsLoader<TContext> parent, ICurrentUserProvider userProvider, bool useUtc):base(parent)
        {
            this.userProvider = userProvider;
            this.useUtc = useUtc;
        }
        protected override void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> builder)
        {
            builder.AddInterceptors(new ModCreateInterceptor(userProvider,useUtc));
        }
    }
}
