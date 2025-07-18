using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Cookies
{
    public class UserDataContainer<T>
    {
        public string ExpectedUserName { get; set; }

        public T Data { get; set; }
}
}
