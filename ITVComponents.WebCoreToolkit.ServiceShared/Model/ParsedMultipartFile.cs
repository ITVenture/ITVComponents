using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Model
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
