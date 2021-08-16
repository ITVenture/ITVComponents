using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public class CustomFilter<T> : FilterBase
    {
        public Expression<Func<T, bool>> Filter { get; set; }
    }
}