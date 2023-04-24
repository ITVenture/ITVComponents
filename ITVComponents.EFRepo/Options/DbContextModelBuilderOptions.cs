using System;
using System.Collections.Concurrent;
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
    public class DbContextModelBuilderOptions<TContext>: IContextModelBuilderOptions
    {
        private List<IEntityConfigurator> configurators = new();

        private ExpressionFixVisitor globalFilterVisitor = new ExpressionFixVisitor();

        private ConcurrentDictionary<string, Delegate> methodImpl = new ConcurrentDictionary<string, Delegate>();

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

        public void ConfigureMethod<T>(string name, T implementation) where T:Delegate
        {
            methodImpl.TryAdd(name, implementation);
        }

        public T GetMethod<T>(string name) where T : Delegate
        {
            T retVal = default;
            if (methodImpl.TryGetValue(name, out var tmp) && tmp is T r)
            {
                retVal = r;
            }

            return retVal;
        }
    }
}
