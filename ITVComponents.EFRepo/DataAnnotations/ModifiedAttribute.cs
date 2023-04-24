using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ModifiedAttribute: ModMarkerAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class CreatedAttribute : ModMarkerAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ModifierAttribute : ModMarkerAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class CreatorAttribute : ModMarkerAttribute
    {
    }
}
