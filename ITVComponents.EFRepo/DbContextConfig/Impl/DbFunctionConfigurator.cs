using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITVComponents.EFRepo.DbContextConfig.Impl
{
    public class DbFunctionConfigurator:IEntityConfigurator
    {
        private readonly MethodInfo method;
        private readonly Action<DbFunctionBuilder> configure;

        public DbFunctionConfigurator(MethodInfo method, Action<DbFunctionBuilder> configure = null)
        {
            this.method = method;
            this.configure = configure;
        }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            if (configure != null)
            {
                modelBuilder.HasDbFunction(method, configure);
            }
            else
            {
                modelBuilder.HasDbFunction(method);
            }
        }
    }
}
