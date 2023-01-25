using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DbContextConfig;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.DbContextConfig.Impl;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.Options
{
    public class DbContextModelBuilderOptions<TContext>
    {
        private List<IEntityConfigurator> configurators = new();

        private ExpressionFixVisitor globalFilterVisitor = new ExpressionFixVisitor();

        public void ConfigureGlobalFilter<T>(Expression<Func<T, bool>> filter) where T : class
        {
            configurators.Add(new FilterConfigurator<T>(filter, globalFilterVisitor));
        }

        public void ConfigureExpressionProperty<T>(Expression<Func<T>> propertyAccess)
        {
            globalFilterVisitor.RegisterExpression(propertyAccess);
        }

        public void ConfigureComputedColumn<T, TProperty>(Expression<Func<T, TProperty>> propertyAccess, string customSql) where T : class
        {
            configurators.Add(new ComputedColumnConfigurator<T, TProperty>(customSql, propertyAccess));
        }

        public void AddCustomConfigurator(IEntityConfigurator customConfigurator)
        {
            configurators.Add(customConfigurator);
        }

        public void ConfigureModelBuilder(ModelBuilder modelBuilder)
        {
            foreach (var configurator in configurators)
            {
                configurator.ConfigureEntity(modelBuilder);
            }
        }
    }
}
