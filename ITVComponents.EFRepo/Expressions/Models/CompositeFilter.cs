using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ITVComponents.EFRepo.Expressions.Models
{
    public class CompositeFilter : FilterBase
    {
        public BoolOperator Operator { get; set; }

        public FilterBase[] Children { get; set; }
    }

    public enum BoolOperator
    {
        And,
        Or
    }
}