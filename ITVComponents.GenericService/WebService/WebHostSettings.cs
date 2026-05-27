using System.Collections.Generic;

namespace ITVComponents.GenericService.WebService
{
    public class WebHostSettings
    {
        public bool TrustAllCertificates { get; set; }

        public List<string> TrustedCertificates { get; set; } = new List<string>();


    }
}