using System;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.EFRepo.DIIntegration
{
    public abstract class ContextOptionsLoader<TContext>:IPlugin where TContext : DbContext
    {
        /*private readonly string targetSetting;
        private readonly IStringFormatProvider connectStringFormatter;
        private readonly int recursionDepth;
        private bool useDi = false;*/
        private readonly string connectString;


        /*protected ContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter, int recursionDepth)
        {
            this.targetSetting = targetSetting;
            this.connectStringFormatter = connectStringFormatter;
            this.recursionDepth = recursionDepth;
            useDi = true;
        }*/

        protected ContextOptionsLoader(string connectString)
        {
            this.connectString = connectString;
        }

        public DbContextOptions Options => BuildOptions();

        private DbContextOptions BuildOptions()
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            //var connectString = useDi?connectStringFormatter.ProcessLiteral($"£[{targetSetting}]{(recursionDepth>1?$"{{{recursionDepth}}}":"")}", null):this.connectString;
            builder.ReplaceService<IModelCacheKeyFactory, CustomModelCache>();
            return ConfigureDb(builder, connectString);
        }

        protected abstract DbContextOptions ConfigureDb(DbContextOptionsBuilder<TContext> builder, string connectString);

        public string UniqueName { get; set; }

        public void Dispose()
        {
            OnDisposed();
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Disposed;
    }
}
