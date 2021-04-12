using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources
{
    public interface IWrappedFkSource
    {
        IEnumerable ReadForeignKey(string tableName, string id = null, Dictionary<string, object> postedFilter = null);
    }
}
