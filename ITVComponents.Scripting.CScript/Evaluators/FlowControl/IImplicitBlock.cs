using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Evaluators.FlowControl
{
    /// <summary>
    /// Marker interface for Evaluators that implicitly open inner scopes to prevent blocks from doing so
    /// </summary>
    public interface IImplicitBlock
    {
    }
}
