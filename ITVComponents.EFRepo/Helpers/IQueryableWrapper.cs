using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Helpers
{
    public interface IQueryableWrapper
    {
        IQueryable Decorated { get; }

        IQueryable<TResult> Select<TResult>(Expression selectExpression);
    }
}
