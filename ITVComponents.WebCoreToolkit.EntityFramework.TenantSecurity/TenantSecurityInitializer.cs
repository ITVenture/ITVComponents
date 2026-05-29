using System;
using ITVComponents.DuckTyping.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Initialization;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity
{
    /// <summary>
    /// Central context-type initialization for the consolidated tenant-security package. Both the
    /// <see cref="WebPartInit"/> and the provider packages (SqlServer/PostgreSql) call into this to wire the
    /// DuckTyping-based <see cref="IDependencyInitializer"/> for the active (identity, tenant) combination.
    /// Replaces the per-strategy WebPartInit.SetContextType/DependencyInit/ContextTypeInitialized members.
    /// </summary>
    public static class TenantSecurityInitializer
    {
        private static IDependencyInitializer init;

        /// <summary>The initializer bound to the active security context (null until initialized).</summary>
        public static IDependencyInitializer DependencyInit => init;

        /// <summary>Indicates whether the context type has already been wired.</summary>
        public static bool ContextTypeInitialized { get; private set; }

        /// <summary>
        /// Wires the dependency initializer for the given (<paramref name="identity"/>, <paramref name="strategy"/>)
        /// combination. When <paramref name="contextType"/> is null, the default context for the combination is used.
        /// </summary>
        public static void SetContextType(Type contextType, IdentityStrategy identity, TenantStrategy strategy)
        {
            if (ContextTypeInitialized)
            {
                throw new InvalidOperationException("ContextType already initialized!");
            }

            Type defaultType = (identity, strategy) switch
            {
                (IdentityStrategy.CoreIdentity, TenantStrategy.Flat) => typeof(CoreIdentity.AspNetSecurityContext),
                (IdentityStrategy.CoreIdentity, TenantStrategy.Tree) => typeof(CoreIdentityTree.AspNetTreeSecurityContext),
                (IdentityStrategy.BasicTenantSecurity, TenantStrategy.Flat) => typeof(Basic.SecurityContext),
                _ => throw new NotSupportedException(
                    $"The tenant-security combination Identity={identity} / Strategy={strategy} is not supported yet.")
            };

            var t = contextType ?? defaultType;
            (string name, Type type)[] fxparam = [(name: "TContext", type: t), (name: "TImpl", type: t)];

            switch (identity, strategy)
            {
                case (IdentityStrategy.CoreIdentity, TenantStrategy.Flat):
                    init = typeof(CoreIdentity.Extensions.DependencyExtensions).WrapType<IDependencyInitializer>(t, fixParameters: fxparam);
                    init.ExtendWithStatic(typeof(Shared.Extensions.DependencyExtensions), t, fixParameters: fxparam);
                    break;
                case (IdentityStrategy.CoreIdentity, TenantStrategy.Tree):
                    init = typeof(CoreIdentityTree.Extensions.DependencyExtensions).WrapType<IDependencyInitializer>(t, fixParameters: fxparam);
                    init.ExtendWithStatic(typeof(TreeShared.Extensions.DependencyExtensions), t, fixParameters: fxparam);
                    init.ExtendWithStatic(typeof(Shared.Extensions.DependencyExtensions), t, fixParameters: fxparam);
                    break;
                case (IdentityStrategy.BasicTenantSecurity, TenantStrategy.Flat):
                    init = typeof(Basic.Extensions.DependencyExtensions).WrapType<IDependencyInitializer>(t, fixParameters: fxparam);
                    init.ExtendWithStatic(typeof(Shared.Extensions.DependencyExtensions), t, fixParameters: fxparam);
                    break;
                default:
                    throw new NotSupportedException(
                        $"The tenant-security combination Identity={identity} / Strategy={strategy} is not supported yet.");
            }

            ContextTypeInitialized = true;
        }
    }
}
