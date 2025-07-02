using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DbContextConfig;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.DbContextConfig.Impl;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DbFunctionAttribute = ITVComponents.EFRepo.DataAnnotations.DbFunctionAttribute;

namespace ITVComponents.EFRepo.Options
{
    public class DbContextModelBuilderOptions<TContext>: IContextModelBuilderOptions
    {
        private List<IEntityConfigurator> configurators = new();

        private ExpressionFixVisitor globalFilterVisitor = new ExpressionFixVisitor();

        private ConcurrentDictionary<string, ConcurrentDictionary<Type, Delegate>> methodImpl = new ConcurrentDictionary<string, ConcurrentDictionary<Type, Delegate>>();

        public void ConfigureGlobalFilter<T>(Expression<Func<T, bool>> filter) where T : class
        {
            ConfigureGlobalFilter(filter, null);
        }

        public void ConfigureGlobalFilter<T>(Expression<Func<T, bool>> filter, Action<EntityTypeBuilder<T>> basicConfig = null) where T : class
        {
            if (basicConfig == null)
            {
                configurators.Add(new FilterConfigurator<T>(filter, globalFilterVisitor));
            }
            else
            {
                configurators.Add(new FilterConfigurator<T>(basicConfig, filter, globalFilterVisitor));
            }
        }

        public void ConfigureEntity<T>(Action<EntityTypeBuilder<T>> basicConfig) where T : class
        {
            configurators.Add(new EntityConfigurator<T>(basicConfig));
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
            var impList = methodImpl.GetOrAdd(name, k => new ConcurrentDictionary<Type, Delegate>());
            if (!impList.TryAdd(typeof(T), implementation))
            {
                throw new InvalidOperationException($"A method with the name {name} and type {typeof(T).Name} is already registered!");
            }
        }

        public T GetMethod<T>(string name) where T : Delegate
        {
            T retVal = default;
            if (methodImpl.TryGetValue(name, out var tmp) && tmp.TryGetValue(typeof(T), out var mt) && mt is T r)
            {
                retVal = r;
            }

            return retVal;
        }

        public void ConfigureDbFunction(string name, Action<DbFunctionBuilder> configure = null)
        {
            var fx = typeof(TContext).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | BindingFlags.InvokeMethod)
                .First(n => Attribute.IsDefined(n,typeof(DbFunctionAttribute), true) && ((DbFunctionAttribute)Attribute.GetCustomAttribute(n, typeof(DbFunctionAttribute)))?.FunctionName == name);
            configurators.Add(new DbFunctionConfigurator(fx, configure));
        }
    }
}
