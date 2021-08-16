using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public class CompareFilter : FilterBase
    {
        public CompareOperator Operator { get; set; }

        public string PropertyName { get; set; }

        public object Value { get; set; }

        public object Value2 { get; set; }
    }

    public enum CompareOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Contains,
        ContainsNot,
        Between,
        NotBetween
    }
}