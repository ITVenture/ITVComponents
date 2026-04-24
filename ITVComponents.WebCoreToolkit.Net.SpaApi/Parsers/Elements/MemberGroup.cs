using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Elements
{
    internal class MemberGroup: IParsedElement
    {
        public bool IsOr { get; }

        public MemberGroup(bool isOr)
        {
            IsOr = isOr;
        }
        public IList<IParsedElement> Elements { get; } = new List<IParsedElement>();
        public FilterBase ToFilter()
        {
            if (Elements.Count == 1)
            {
                return Elements[0].ToFilter();
            }

            var retVal = new CompositeFilter { Operator = IsOr ? BoolOperator.Or : BoolOperator.And };
            retVal.Children = Elements.Select(n => n.ToFilter()).ToArray();
            return retVal;
        }
    }
}
