using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources.Impl
{
    internal class WrappedCustomFkSource:IWrappedFkSource
    {
        private readonly IForeignKeyProvider foreignKeyProvider;

        public WrappedCustomFkSource(IForeignKeyProvider foreignKeyProvider)
        {
            this.foreignKeyProvider = foreignKeyProvider;
        }
        
        public IEnumerable ReadForeignKey(string tableName, string id = null, Dictionary<string, object> postedFilter = null)
        {
            if (id == null)
            {
                return foreignKeyProvider.GetForeignKeyFilterQuery(tableName, postedFilter);
            }
            
            return foreignKeyProvider.GetForeignKeyResolveQuery(tableName, id);
        }
    }
}
