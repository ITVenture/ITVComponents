using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model
{
    public interface IParsedElement
    {
        IList<IParsedElement> Elements { get; }

        FilterBase ToFilter();
    }
}
