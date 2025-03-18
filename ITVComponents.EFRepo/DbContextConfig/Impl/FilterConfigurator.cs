using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITVComponents.EFRepo.DbContextConfig.Impl
{
    public class FilterConfigurator<T>:EntityConfigurator<T> where T : class
    {
        private Expression<Func<T, bool>> filter;

        private ExpressionVisitor visitor;

        public FilterConfigurator(Expression<Func<T, bool>> filter, ExpressionVisitor visitor):base(null)
        {
            this.filter = filter;
            this.visitor = visitor;
        }

        public FilterConfigurator(Action<EntityTypeBuilder<T>> baseConfig, Expression<Func<T, bool>> filter, ExpressionVisitor visitor) : base(baseConfig)
        {
            this.filter = filter;
            this.visitor = visitor;
        }

        protected override EntityTypeBuilder<T> ConfigureEntityType(EntityTypeBuilder<T> builder)
        {
            Expression<Func<T, bool>> modifiedFilter = (Expression<Func<T, bool>>)visitor.Visit(filter);
            var cfg = base.ConfigureEntityType(builder);
            cfg.HasQueryFilter(modifiedFilter);
            return cfg;
        }
    }
}
