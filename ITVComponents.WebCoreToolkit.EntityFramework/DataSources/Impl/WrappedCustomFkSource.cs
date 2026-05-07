using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources.Impl
{
    internal class WrappedCustomFkSource:IWrappedFkSource
    {
        private readonly IForeignKeyProvider foreignKeyProvider;

        private readonly IForeignKeyProviderWithOptions cfg;

        public WrappedCustomFkSource(IForeignKeyProvider foreignKeyProvider)
        {
            this.foreignKeyProvider = foreignKeyProvider;
            this.cfg = foreignKeyProvider as IForeignKeyProviderWithOptions;
        }

        public ForeignKeyOptions CustomFkSettings => cfg?.DefaultFkOptions;

        public IEnumerable ReadForeignKey(string tableName, string id = null, Dictionary<string, object> postedFilter = null)
        {
            if (id == null)
            {
                return foreignKeyProvider.GetForeignKeyFilterQuery(tableName, postedFilter, out _);
            }
            
            return foreignKeyProvider.GetForeignKeyResolveQuery(tableName, id, out _);
        }

        public IEnumerable<ForeignKeyData<T>> ReadForeignKey<T>(string tableName, string id = null, Dictionary<string, object> postedFilter = null)
        {
            if (id == null)
            {
                return foreignKeyProvider.GetForeignKeyFilterQuery(tableName, postedFilter, out var pkType).CastForeignKey<T>(pkType);
            }

            return foreignKeyProvider.GetForeignKeyResolveQuery(tableName, id, out var pt).CastForeignKey<T>(pt);
        }   
    }
}
