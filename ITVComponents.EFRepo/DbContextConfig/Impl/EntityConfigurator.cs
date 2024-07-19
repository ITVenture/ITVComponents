using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITVComponents.EFRepo.DbContextConfig.Impl
{
    public class EntityConfigurator<T> : IEntityConfigurator where T : class
    {
        private readonly Action<EntityTypeBuilder<T>> entityConfiguration;

        public EntityConfigurator(Action<EntityTypeBuilder<T>>? entityConfiguration)
        {
            this.entityConfiguration = entityConfiguration;
        }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            var cfg = modelBuilder.Entity<T>();
            ConfigureEntityType(cfg);
        }

        protected virtual EntityTypeBuilder<T> ConfigureEntityType(EntityTypeBuilder<T> builder)
        {
            var cfg = builder;
            if (entityConfiguration != null)
            {
                entityConfiguration(cfg);
            }

            return cfg;
        }
    }
}
