using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources.Impl
{
    internal class WrappedDbContext:IWrappedDataSource
    {
        private readonly DbContext decoratedContext;
        private readonly IServiceProvider services;
        private readonly IForeignKeyProviderWithOptions cfg;

        public WrappedDbContext(DbContext decoratedContext, IServiceProvider services)
        {
            this.decoratedContext = decoratedContext;
            this.cfg = decoratedContext as IForeignKeyProviderWithOptions;
            this.services = services;
        }
        public IEnumerable RunDiagnosticsQuery(DiagnosticsQueryDefinition qr, ClaimsPrincipal user, IServiceProvider services, IDictionary<string, string> queryArguments)
        {
            return decoratedContext.RunDiagnosticsQuery(user, services, qr, queryArguments);
        }

        public IEnumerable RunDiagnosticsQuery(DiagnosticsQueryDefinition query, ClaimsPrincipal user, IServiceProvider services, IDictionary<string, object> arguments)
        {
            return decoratedContext.RunDiagnosticsQuery(user, services, query, arguments);
        }

        public ForeignKeyOptions CustomFkSettings => cfg?.DefaultFkOptions;

        public IEnumerable ReadForeignKey(string tableName, string id = null, Dictionary<string, object> postedFilter = null)
        {
            return decoratedContext.ReadForeignKey(tableName, services, id, postedFilter);
        }

        public IEnumerable<ForeignKeyData<T>> ReadForeignKey<T>(string tableName, string id = null, Dictionary<string, object> postedFilter = null)
        {
            return decoratedContext.ReadForeignKey<T>(tableName, services, id, postedFilter);
        }
    }
}
