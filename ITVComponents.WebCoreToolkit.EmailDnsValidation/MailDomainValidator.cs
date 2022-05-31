using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DnsClient;
using ITVComponents.WebCoreToolkit.Helpers;

namespace ITVComponents.WebCoreToolkit.EmailDnsValidation
{
    public class MailDomainValidator:IMailDomainVerifier
    {
        private readonly LookupClientOptions options;

        public MailDomainValidator()
        {
        }

        public MailDomainValidator(LookupClientOptions options)
        {
            this.options = options;
        }

        public bool DomainValid(string domain)
        {
            var client = options == null ? new LookupClient() : new LookupClient(options);
            var result = client.Query(domain, QueryType.MX);
            return result.Answers.MxRecords().Any();
        }
    }
}
