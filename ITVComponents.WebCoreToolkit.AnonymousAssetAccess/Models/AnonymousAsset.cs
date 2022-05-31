using System;

namespace ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Models
{
    public class AnonymousAsset
    {
        public AnonymousAsset(string key, DateTime created)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Created = created;
        }

        public string Key { get; }
        public DateTime Created { get; }
    }
}
