using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DbContextConfig.Impl
{
    public class TableNamesFromProperties<TContext>:IEntityConfigurator where TContext:DbContext
    {
        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.TableNamesFromProperties(typeof(TContext));
        }
    }
}
