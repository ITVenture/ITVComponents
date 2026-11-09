using System;

namespace ITVComponents.WebCoreToolkit.WebPlugins.ServiceModels
{
    internal class BufferedObject
    {
        public BufferedObject(object value, DateTime validThrough, DateTime loadStartedUtc)
        {
            Value = value;
            ValidThrough = validThrough;
            LoadStartedUtc = loadStartedUtc;
        }

        public object Value { get; }

        public DateTime ValidThrough { get; }

        /// <summary>
        /// When reading the value began. A change to the source written after this point may not be in
        /// <see cref="Value"/>, so the entry counts as stale once the source reports a later change.
        /// </summary>
        public DateTime LoadStartedUtc { get; }
    }
}
