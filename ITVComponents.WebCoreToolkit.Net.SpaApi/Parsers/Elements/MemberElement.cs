using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Elements
{
    internal class MemberElement:IFilterElement
    {
        public StringBuilder Content { get; } = new StringBuilder();

        public StringBuilder Operator { get; } = new StringBuilder();

        public StringBuilder Value { get; } = new StringBuilder();

        public StringBuilder Value2 { get; } = new StringBuilder();

        public bool IsFinal => Content.Length > 0 && FilterLexer.JsOperators.Contains(Operator.ToString())
                                                  && (Value.Length > 0 ||
                               FilterLexer.JsNoOpPerators.Contains(Operator.ToString())) &&
                                                  (Value2.Length > 0 || !FilterLexer.JsTwoOpOperators.Contains(Operator.ToString()));
    }
}
