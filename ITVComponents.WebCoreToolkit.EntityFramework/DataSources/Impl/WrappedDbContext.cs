using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources.Impl
{
    internal class WrappedDbContext:IWrappedDataSource
    {
        private readonly DbContext decoratedContext;

        public WrappedDbContext(DbContext decoratedContext)
        {
            this.decoratedContext = decoratedContext;
        }
        public IEnumerable RunDiagnosticsQuery(DiagnosticsQueryDefinition qr, IDictionary<string, string> queryArguments)
        {
            return decoratedContext.RunDiagnosticsQuery(qr, queryArguments);
        }

        public IEnumerable RunDiagnosticsQuery(DiagnosticsQueryDefinition query, IDictionary<string, object> arguments)
        {
            return decoratedContext.RunDiagnosticsQuery(query, arguments);
        }

        public IEnumerable ReadForeignKey(string tableName, string id = null, Dictionary<string, object> postedFilter = null)
        {
            return decoratedContext.ReadForeignKey(tableName, id, postedFilter);
        }
    }
}
