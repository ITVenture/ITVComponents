using ITVComponents.EFRepo.DbContextConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Options
{
    public interface IContextModelBuilderOptions
    {
        public void ConfigureGlobalFilter<T>(Expression<Func<T, bool>> filter) where T : class;

        public void ConfigureGlobalFilter<T>(Expression<Func<T, bool>> filter,
            Action<EntityTypeBuilder<T>> basicConfig) where T : class;

        public void ConfigureEntity<T>(Action<EntityTypeBuilder<T>> basicConfig) where T : class;

        public void ConfigureExpressionProperty<T>(Expression<Func<T>> propertyAccess);

        public void ConfigureComputedColumn<T, TProperty>(Expression<Func<T, TProperty>> propertyAccess,
            string customSql) where T : class;

        public void AddCustomConfigurator(IEntityConfigurator customConfigurator);

        public void ConfigureModelBuilder(ModelBuilder modelBuilder);

        public void ConfigureMethod<T>(string name, T implementation) where T:Delegate;

        public T GetMethod<T>(string name) where T : Delegate;
    }
}
