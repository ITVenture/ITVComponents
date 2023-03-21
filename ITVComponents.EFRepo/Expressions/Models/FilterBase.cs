using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public abstract class FilterBase
    {
        protected abstract string DescribeFilter();
        public override string ToString()
        {
            return DescribeFilter();
        }
    }
}