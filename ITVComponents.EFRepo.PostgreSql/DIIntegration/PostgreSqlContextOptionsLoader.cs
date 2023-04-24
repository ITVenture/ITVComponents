using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.Plugins.Initialization;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.PostgreSql.DIIntegration
{
    public class PostgreSqlContextOptionsLoader<TContext>:ContextOptionsLoader<TContext> where TContext : DbContext
    {
        private readonly string connectString;

        private bool useProxies;
        /*public PostgreSqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter, bool useProxies) : base(targetSetting, connectStringFormatter, 1)
        {
            this.useProxies = useProxies;
        }*/

        /*public PostgreSqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter,
            int recursionDepth, bool useProxies) : base(targetSetting, connectStringFormatter, recursionDepth)
        {
            this.useProxies = useProxies;
        }*/

        public PostgreSqlContextOptionsLoader(string connectString, bool useProxies) : base()
        {
            this.connectString = connectString;
            this.useProxies = useProxies;
        }

        protected override void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> builder)
        {
            builder.UseNpgsql(connectString);
            if (useProxies)
            {
                builder.UseLazyLoadingProxies();
            }
        }
    }
}
