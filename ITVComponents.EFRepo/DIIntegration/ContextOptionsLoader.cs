using System;
using System.Collections.Generic;
using Dynamitey.DynamicObjects;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.EFRepo.DIIntegration
{
    public abstract class ContextOptionsLoader<TContext>:IPlugin where TContext : DbContext
    {
        private List<ContextOptionsLoader<TContext>> innerLoaders = new();
        //private readonly string connectString;


        protected ContextOptionsLoader()
        {
            //this.connectString = connectString;
        }

        protected ContextOptionsLoader(ContextOptionsLoader<TContext> parent)
        {
            parent.RegisterInnerLoader(this);
        }

        private void RegisterInnerLoader(ContextOptionsLoader<TContext> contextOptionsLoader)
        {
            innerLoaders.Add(contextOptionsLoader);
        }

        public DbContextOptions Options => BuildOptions();

        private DbContextOptions BuildOptions()
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            builder.ReplaceService<IModelCacheKeyFactory, CustomModelCache>();
            return ConfigureDb(builder);
        }

        public string UniqueName { get; set; }

        public void Dispose()
        {
            OnDisposed();
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        protected abstract void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> builder);

        private DbContextOptions ConfigureDb(DbContextOptionsBuilder<TContext> builder)
        {
            ConfigureOptionsBuilder(builder);
            innerLoaders.ForEach(n => n.ConfigureOptionsBuilder(builder));
            return builder.Options;
        }

        public event EventHandler Disposed;
    }
}
