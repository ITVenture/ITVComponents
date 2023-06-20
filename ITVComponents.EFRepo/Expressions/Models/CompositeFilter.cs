using ITVComponents.Helpers;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ITVComponents.EFRepo.Expressions.Models
{
    public class CompositeFilter : FilterBase
    {
        private List<FilterBase> filters = new();

        private FilterBase[] builtFilter;

        public BoolOperator Operator { get; set; }

        public FilterBase[] Children
        {
            get
            {
                return builtFilter ??= filters.ToArray();
            }
            set
            {
                filters.Clear();
                filters.AddRange(value);
            }
        }

        protected override string DescribeFilter()
        {
            return JsonHelper.ToJson(new
            {
                Operator = Operator.ToString(),
                Type = "Composite",
                Children = (from t in Children select t.ToString()).ToArray()
            });
        }

        public void AddFilter(FilterBase filter)
        {
            filters.Add(filter);
            builtFilter = null;
        }
    }

    public enum BoolOperator
    {
        And,
        Or
    }
}