using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources
{
    public interface IWrappedDataSource: IWrappedFkSource
    {
        IEnumerable RunDiagnosticsQuery(DiagnosticsQueryDefinition qr, IDictionary<string, string> queryArguments);

        IEnumerable RunDiagnosticsQuery(DiagnosticsQueryDefinition query, IDictionary<string, object> arguments);
    }
}
