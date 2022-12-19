using ITVComponents.EFRepo.DynamicData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.DynamicData.Models
{
    public class RequestModel<T> where T: class
    {
        public FilterModel Filters { get; set; }

        public DynamicTableSort[] Sorts { get; set; }

        public string[] CustomFilters { get; set; }

        public T[] Items { get; set; }

        public int? Page { get; set; }

        public int? PageSize { get; set; }
    }
}
