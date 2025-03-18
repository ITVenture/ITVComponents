using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
{
    public class TagHelperDataViewModel : IEquatable<TagHelperDataViewModel>
    {
        public string FullName { get; set; }
        public string AssemblyLocation { get; set; }
        public string TargetTag { get; set; }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is TagHelperDataViewModel other && Equals(other);
        }

        public bool Equals(TagHelperDataViewModel other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(FullName, other.FullName, StringComparison.OrdinalIgnoreCase) && string.Equals(AssemblyLocation, other.AssemblyLocation, StringComparison.OrdinalIgnoreCase) && string.Equals(TargetTag, other.TargetTag, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(FullName, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(AssemblyLocation, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(TargetTag, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }

        public static bool operator ==(TagHelperDataViewModel left, TagHelperDataViewModel right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TagHelperDataViewModel left, TagHelperDataViewModel right)
        {
            return !Equals(left, right);
        }
    }
}
