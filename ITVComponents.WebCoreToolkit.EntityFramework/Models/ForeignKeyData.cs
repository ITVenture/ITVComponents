using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class ForeignKeyData<TKey>
    {
        public TKey Key { get; set; }

        public string Label { get; set; }

        public IDictionary<string,object> FullRecord { get; set; }
    }
}
