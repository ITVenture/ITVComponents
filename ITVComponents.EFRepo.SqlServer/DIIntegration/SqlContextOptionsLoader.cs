using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.Plugins.Initialization;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.SqlServer.DIIntegration
{
    public class SqlContextOptionsLoader<TContext>:ContextOptionsLoader<TContext> where TContext : DbContext
    {
        private readonly string connectString;

        private bool useProxies;

        private bool useCommandTimeout = false;

        private int commandTimeout;
        /*public SqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter, bool useProxies) : base(targetSetting, connectStringFormatter, 1)
        {
            this.useProxies = useProxies;
        }

        public SqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter, int recursionDepth, bool useProxies) : base(targetSetting, connectStringFormatter, recursionDepth)
        {
            this.useProxies = useProxies;
        }*/

        public SqlContextOptionsLoader(string connectString, bool useProxies, int commandTimeout):base()
        {
            this.connectString = connectString;
            this.useProxies = useProxies;
            if (commandTimeout >= 0)
            {
                useCommandTimeout = true;
                this.commandTimeout = commandTimeout;
            }
        }

        protected override void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> builder)
        {
            builder.UseSqlServer(connectString, o =>
            {
                if (useCommandTimeout)
                {
                    o.CommandTimeout(commandTimeout);
                }
            });
            if (useProxies)
            {
                builder.UseLazyLoadingProxies();
            }
        }
    }
}
