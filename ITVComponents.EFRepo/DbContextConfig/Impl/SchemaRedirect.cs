using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DbContextConfig.Impl
{
    public class SchemaRedirect: IEntityConfigurator
    {
        private string targetSchema;

        public SchemaRedirect(string targetSchema)
        {
            this.targetSchema = targetSchema;
        }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(targetSchema);
        }
    }
}
