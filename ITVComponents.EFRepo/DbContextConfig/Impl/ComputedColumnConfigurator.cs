using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DbContextConfig.Impl
{
    public class ComputedColumnConfigurator<T, TProperty>:IEntityConfigurator where T : class
    {
        private readonly string customSql;
        private readonly Expression<Func<T, TProperty>> propertyFunc;

        public ComputedColumnConfigurator(string customSql, Expression<Func<T, TProperty>> propertyFunc)
        {
            this.customSql = customSql;
            this.propertyFunc = propertyFunc;
        }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<T>().Property(propertyFunc).HasComputedColumnSql(customSql);
        }
    }
}
