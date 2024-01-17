using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Visitors;
using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.EFRepo.Internal
{
    internal class QueryableDecorator<T>:IQueryableWrapper, IQueryable<T> where T : class, new()
    {
        private readonly IQueryable<T> decorated;

        private readonly LinqVisitor visitor = new LinqVisitor();

        public QueryableDecorator(IQueryable<T> decorated)
        {
            this.decorated = decorated;
        }

        public IQueryable Decorated => decorated;

        public IQueryable<TResult> Select<TResult>(Expression selectExpression)
        {
            var visitedEx = visitor.Visit(selectExpression);
            var selection = (Expression<Func<T, TResult>>)visitedEx;
            return decorated.Select(selection);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return decorated.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Type ElementType => decorated.ElementType;
        public Expression Expression => decorated.Expression;
        public IQueryProvider Provider => decorated.Provider;
    }
}
