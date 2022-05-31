using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public sealed class FullSecurityAccessHelper:IDisposable
    {
        private readonly IBaseTenantContext db;
        public FullSecurityAccessHelper ForwardHelper { get; set; }

        public bool HideGlobals{ get; set; }
        public bool ShowAllTenants{ get; set; }

        public bool CreatedWithContext { get; }

        public FullSecurityAccessHelper()
        {
        }

        internal FullSecurityAccessHelper(IBaseTenantContext db, bool allTenants, bool hideGlobals)
        {
            this.db = db;
            HideGlobals = hideGlobals;
            ShowAllTenants = allTenants;
            CreatedWithContext = true;
            db.RegisterSecurityRollback(this);
        }

        public static FullSecurityAccessHelper CreateForCaller(IBaseTenantContext db, bool allTenants, bool hideGlobals)
        {
            var stack = new StackTrace(new StackFrame(1, false));
            var type = stack.GetFrame(0).GetMethod().DeclaringType;
            var cmp = db.TrustedFullAccessComponents.FirstOrDefault(n =>
                n.FullQualifiedTypeName == type.AssemblyQualifiedName);
            if (cmp != null)
            {
                return new FullSecurityAccessHelper(db, allTenants, hideGlobals);
            }

            throw new InvalidOperationException($"The caller ({type.AssemblyQualifiedName}) is not trusted!");
        }

        public void Dispose()
        {
            db.RollbackSecurity(this);
        }
            
    }
}
