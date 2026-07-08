using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Typed tenant-template part payload for the <c>EmployeeRoleMapping</c> catalog. Registered for native
    /// polymorphism under <see cref="EmployeeRoleMappingTemplateEntry.PartKey"/>.
    /// </summary>
    public class EmployeeRoleMappingTemplatePayload : TemplateExtensionPayload
    {
        public List<EmployeeRoleMappingTemplateEntry> Mappings { get; set; } = new();
    }
}
