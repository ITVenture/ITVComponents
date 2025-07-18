using ITVComponents.Json;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Antlr4.Runtime.Atn.SemanticContext;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public class CustomFilter<T> : FilterBase, IExpressionFilter
    {
        public Expression FilterExpression => Filter;
        public Expression<Func<T, bool>> Filter { get; set; }

        protected override string DescribeFilter()
        {
            return JsonHelper.ToJson(new
            {
                Type = "Lambda",
                Filter = Filter?.ToString()
            }, SerializationTypingMode.StaticTyping, null);
        }
    }
}