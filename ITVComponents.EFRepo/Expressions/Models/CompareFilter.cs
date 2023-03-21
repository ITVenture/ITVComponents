using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public class CompareFilter : FilterBase
    {
        public CompareOperator Operator { get; set; }

        public string PropertyName { get; set; }

        public object Value { get; set; }

        public object Value2 { get; set; }

        protected override string DescribeFilter()
        {
            return JsonHelper.ToJson(new
            {
                PropertyName,
                Value,
                Value2,
                Operator = Operator.ToString(),
                Type = "Comparer"
            });
        }
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
        StartsWith,
        StartsNotWith,
        EndsWith,
        EndsNotWith,
        Between,
        NotBetween,
        IsNull,
        IsNotNull,
        IsEmpty,
        IsNotEmpty
    }
}