using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model
{
    internal interface IFilterElement
    {
        StringBuilder Content { get; }

        bool IsFinal { get; }
    }

    internal enum FilterElementMode{
        Content,
        Operator,
        Value,
        Value2
    }
}
