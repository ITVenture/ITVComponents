using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Models
{
    public class ApiKey
    {
        public ApiKey(string key, DateTime created)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Created = created;
        }

        public string Key { get; }
        public DateTime Created { get; }
    }
}
