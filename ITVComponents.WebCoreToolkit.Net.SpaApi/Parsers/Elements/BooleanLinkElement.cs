using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Elements
{
    internal class BooleanLinkElement: IFilterElement
    {
        public StringBuilder Content { get; } = new StringBuilder();
        public bool IsFinal => Content.ToString() is "and" or "or";
        public bool And => Content.ToString() is "and";
    }
}
