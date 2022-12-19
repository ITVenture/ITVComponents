using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class ConservedSession:ISession
    {
        private Dictionary<string, byte[]> data = new Dictionary<string, byte[]>();
        public ConservedSession(ISession original)
        {
            IsAvailable = original.IsAvailable;
            if (IsAvailable)
            {
                foreach (var s in original.Keys)
                {
                    if (original.TryGetValue(s, out var b))
                    {
                        data[s] = b;
                    }
                }
            }

            Id = original.Id;
        }

        public ConservedSession()
        {
            IsAvailable = false;
        }


        public Task LoadAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.CompletedTask;
        }

        public bool TryGetValue(string key, out byte[] value)
        {
            return data.TryGetValue(key, out value);
        }

        public void Set(string key, byte[] value)
        {
            data[key] = value;
        }

        public void Remove(string key)
        {
            data.Remove(key);
        }

        public void Clear()
        {
            data.Clear();
        }

        public bool IsAvailable { get; }
        public string Id { get; }
        public IEnumerable<string> Keys => data.Keys;
    }
}
