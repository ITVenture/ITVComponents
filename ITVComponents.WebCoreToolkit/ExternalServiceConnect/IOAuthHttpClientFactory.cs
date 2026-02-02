using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect
{
    public interface IOAuthHttpClientFactory
    {
        HttpClient Create(string connectionName);
    }
}
