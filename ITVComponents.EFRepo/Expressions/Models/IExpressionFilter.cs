using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public interface IExpressionFilter
    {
        Expression FilterExpression { get; }
    }
}
