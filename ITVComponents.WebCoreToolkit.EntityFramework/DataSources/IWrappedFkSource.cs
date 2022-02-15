using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources
{
    public interface IWrappedFkSource
    {
        ForeignKeyOptions CustomFkSettings { get; }
        IEnumerable ReadForeignKey(string tableName, string id = null, Dictionary<string, object> postedFilter = null);
    }
}
