using ITVComponents.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core.Native;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public class LinqFilter<T>:FilterBase
    {
        private readonly string filterText;
        private readonly string configurationName;
        private Expression<Func<T, bool>> filter;

        public LinqFilter(string filterText, string configurationName)
        {
            this.filterText = filterText;
            this.configurationName = configurationName;
        }

        public Expression<Func<T, bool>> Filter => filter ??= BuildFilter();

        private Expression<Func<T, bool>> BuildFilter()
        {
            return NativeScriptHelper.CompileExpression<Func<T, bool>>(configurationName, filterText);
        }

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
