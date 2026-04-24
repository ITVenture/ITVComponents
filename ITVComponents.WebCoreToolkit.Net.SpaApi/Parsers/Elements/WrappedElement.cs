using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Elements
{
    internal class WrappedElement:IParsedElement
    {
        public IList<IParsedElement> Elements { get; }
        public FilterBase ToFilter()
        {
            var member = (MemberElement)LexerToken;
            var operatorValue = FilterLexer.JsOperators.Select((v, i) => new { Value = (CompareOperator)i, Name = v })
                .First(n => n.Name == member.Operator.ToString()).Value;
            var retVal = new CompareFilter
            {
                Operator = operatorValue,
                PropertyName = member.Content.ToString(),
                Value = member.Value.ToString(),
                Value2 = member.Value2.ToString()
            };

            return retVal;
        }

        public IFilterElement LexerToken { get; set; }
    }
}
