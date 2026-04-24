using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Handlers.Model
{
    public class DataResult
    {
        public IEnumerable Data { get; set; }
        public int Total { get; set; }
        public object Errors { get; init; } = null;
    }
}
