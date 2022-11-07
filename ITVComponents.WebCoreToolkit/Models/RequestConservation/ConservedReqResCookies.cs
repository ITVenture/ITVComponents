using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class ConservedReqResCookies:IRequestCookieCollection, IResponseCookies
    {
        private Dictionary<string, string> decorated;

        public ConservedReqResCookies(IRequestCookieCollection original)
        {
            decorated = new Dictionary<string, string>(original, StringComparer.OrdinalIgnoreCase);
        }

        public ConservedReqResCookies()
        {
            decorated = new Dictionary<string, string>();
        }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return decorated.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)decorated).GetEnumerator();
        }

        public bool ContainsKey(string key)
        {
            return decorated.ContainsKey(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            return decorated.TryGetValue(key, out value);
        }

        public int Count => decorated.Count;
        public ICollection<string> Keys => decorated.Keys;

        public string this[string key] => decorated[key];
        public void Append(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void Append(string key, string value, CookieOptions options)
        {
            throw new NotImplementedException();
        }

        public void Delete(string key)
        {
            throw new NotImplementedException();
        }

        public void Delete(string key, CookieOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
