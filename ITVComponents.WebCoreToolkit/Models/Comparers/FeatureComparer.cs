using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.Comparers
{
    public class FeatureComparer : IEqualityComparer<Feature>
    {
        public bool Equals(Feature x, Feature y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.FeatureName.Equals(y.FeatureName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(Feature obj)
        {
            return (obj.FeatureName != null ? obj.FeatureName.GetHashCode() : 0);
        }
    }
}
