using System;

namespace ITVComponents.AssemblyResolving
{
    [Serializable]
    public class AssemblyResolverConfigurationItem
    {
        public string Name { get; set; }

        public string Path { get; set; }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                return $"{Name} ({Path})";
            }

            return base.ToString();
        }
    }
}
