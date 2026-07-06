namespace ITVComponents.WebCoreToolkit.Models
{
    /// <summary>
    /// A single node of the lazily-expandable tenant tree a user may switch into. Produced by
    /// <see cref="Security.ISecurityRepository.GetRootTenants"/> (roots) and
    /// <see cref="Security.ISecurityRepository.GetChildTenants"/> (one level per expand). Only tenants the user
    /// can actually access (≥1 permission, directly or via role inheritance) are emitted; pass-through tenants
    /// (a role but no permission) are collapsed, so a node's children are its nearest accessible descendants.
    /// </summary>
    public sealed class TenantTreeNode
    {
        public int TenantId { get; set; }

        public int? ParentTenantId { get; set; }

        public string TenantName { get; set; }

        public string DisplayName { get; set; }

        /// <summary>
        /// True when at least one accessible tenant exists somewhere below this node (through any pass-through
        /// span) — i.e. whether the lazy tree should show an expand affordance for this node.
        /// </summary>
        public bool HasAccessibleChildren { get; set; }

        /// <summary>Whether the user reaches this tenant by a directly assigned role or only via inheritance.</summary>
        public ScopeAccessMode AccessMode { get; set; } = ScopeAccessMode.Direct;

        /// <summary>
        /// Opaque plumbing: the local role-ids the user effectively holds at this node. Carry this back verbatim
        /// into <see cref="Security.ISecurityRepository.GetChildTenants"/> when the node is expanded so the next
        /// level can be resolved without re-walking from the top. It is server-side state (kept in the Blazor
        /// circuit) and has no display meaning.
        /// </summary>
        public int[] CarriedRoleIds { get; set; }
    }
}
