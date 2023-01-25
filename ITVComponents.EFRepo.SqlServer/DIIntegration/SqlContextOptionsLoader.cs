using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.Plugins.Initialization;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.SqlServer.DIIntegration
{
    public class SqlContextOptionsLoader<TContext>:ContextOptionsLoader<TContext> where TContext : DbContext
    {
        private bool useProxies;
        /*public SqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter, bool useProxies) : base(targetSetting, connectStringFormatter, 1)
        {
            this.useProxies = useProxies;
        }

        public SqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter, int recursionDepth, bool useProxies) : base(targetSetting, connectStringFormatter, recursionDepth)
        {
            this.useProxies = useProxies;
        }*/

        public SqlContextOptionsLoader(string connectString, bool useProxies):base(connectString)
        {
            this.useProxies = useProxies;
        }

        protected override DbContextOptions ConfigureDb(DbContextOptionsBuilder<TContext> builder, string connectString)
        {
            
            builder.UseSqlServer(connectString);
            if (useProxies)
            {
                builder.UseLazyLoadingProxies();
            }

            return builder.Options;
        }
    }
}
