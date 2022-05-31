using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Helpers
{
    public interface IMailDomainVerifier
    {
        /// <summary>
        /// Checks whether the given dns is valid and has a mx record
        /// </summary>
        /// <param name="domain">the domain to check</param>
        /// <returns>a value indicating whether the given domain is ok</returns>
        bool DomainValid(string domain);
    }
}
