using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Elements
{
    internal class ParenthesisElement:IFilterElement
    {
        public StringBuilder Content { get; } = new StringBuilder();
        public bool IsFinal => Content.Length == 1;
        public bool Opening => IsFinal && Content.ToString() == "(";
    }
}
