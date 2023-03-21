using ITVComponents.Helpers;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Antlr4.Runtime.Atn.SemanticContext;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public class CustomFilter<T> : FilterBase
    {
        public Expression<Func<T, bool>> Filter { get; set; }

        protected override string DescribeFilter()
        {
            return JsonHelper.ToJson(new
            {
                Type = "Lambda",
                Filter = Filter?.ToString()
            });
        }
    }
}