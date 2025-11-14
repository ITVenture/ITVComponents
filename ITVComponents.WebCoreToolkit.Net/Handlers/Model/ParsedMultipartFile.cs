using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Net.Handlers.Model
{
    public class ParsedMultipartFile
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }

        public bool IsFormData { get; set; }

        public Dictionary<string, StringValues> Headers { get; set; }
    }
}
