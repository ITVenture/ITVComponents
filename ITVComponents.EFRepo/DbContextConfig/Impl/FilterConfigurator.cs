using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DbContextConfig.Impl
{
    public class FilterConfigurator<T>:IEntityConfigurator where T : class
    {
        private Expression<Func<T, bool>> filter;

        private ExpressionVisitor visitor;

        public FilterConfigurator(Expression<Func<T, bool>> filter, ExpressionVisitor visitor)
        {
            this.filter = filter;
            this.visitor = visitor;
        }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            Expression<Func<T, bool>> modifiedFilter = (Expression<Func<T, bool>>)visitor.Visit(filter);
            modelBuilder.Entity<T>().HasQueryFilter(modifiedFilter);
        }
    }
}
