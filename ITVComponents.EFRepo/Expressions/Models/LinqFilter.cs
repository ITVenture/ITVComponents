using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Json;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public class LinqFilter<T>:FilterBase, IExpressionFilter
    {
        private string label;
        private readonly string filterText;
        private readonly string configurationName;
        private Expression<Func<T, bool>> filter;

        public LinqFilter(string label, string filterText, string configurationName)
        {
            this.label = label;
            this.filterText = filterText;
            this.configurationName = configurationName;
        }

        public Expression FilterExpression => Filter;
        public Expression<Func<T, bool>> Filter => filter ??= BuildFilter();

        private Expression<Func<T, bool>> BuildFilter()
        {
            return NativeScriptHelper.CompileExpression<Func<T, bool>>(configurationName, label, filterText);
        }

        protected override string DescribeFilter()
        {
            return JsonHelper.ToJson(new
            {
                Type = "Lambda",
                Filter = Filter?.ToString()
            }, SerializationTypingMode.StaticTyping, null);
        }
    }

    public class LinqFilter : FilterBase, IExpressionFilter
    {
        private readonly string configurationName;
        private Func<string, string, string, Expression> getExpression;
        private Expression expression;
        private readonly string label;
        private string expressionText;
        public LinqFilter(string label, string expression, Type entityType, string configurationName)
        {
            this.label = label;
            expressionText = expression;
            this.configurationName = configurationName;
            var tmpXp= LambdaHelper.GetMethodInfo(() => NativeScriptHelper.CompileExpression<object>(null, null, null));
            getExpression = tmpXp.MakeGenericMethod(typeof(Func<,>).MakeGenericType(entityType, typeof(bool)))
                .CreateDelegate<Func<string, string, string, Expression>>();
        }

        protected override string DescribeFilter()
        {
            return JsonHelper.ToJson(new
            {
                Type = "Lambda",
                Filter = FilterExpression?.ToString()
            }, SerializationTypingMode.StaticTyping, null);
        }

        public Expression FilterExpression => expression ??= getExpression(configurationName, label, expressionText);
    }
}
