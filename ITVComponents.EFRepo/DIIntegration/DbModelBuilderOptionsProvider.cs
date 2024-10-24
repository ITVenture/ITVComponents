﻿using System;
using System.Collections.Generic;
using ITVComponents.EFRepo.Options;
using ITVComponents.Plugins.DIIntegration;
using Microsoft.Extensions.Options;

namespace ITVComponents.EFRepo.DIIntegration
{
    public abstract class DbModelBuilderOptionsProvider<TContext>:IOptionsProvider<DbContextModelBuilderOptions<TContext>>, IOptions<DbContextModelBuilderOptions<TContext>>
    {
        private DbContextModelBuilderOptions<TContext> defaultOptions;

        private List<DbModelBuilderOptionsProvider<TContext>> innerBuilders;

        protected DbModelBuilderOptionsProvider()
        {
            innerBuilders = new List<DbModelBuilderOptionsProvider<TContext>>();
        }

        protected DbModelBuilderOptionsProvider(DbModelBuilderOptionsProvider<TContext> parentBuilder):this()
        {
            parentBuilder.RegisterInnerProvider(this);
        }

        private void RegisterInnerProvider(DbModelBuilderOptionsProvider<TContext> innerBuilder)
        {
            innerBuilders.Add(innerBuilder);
        }

        public string UniqueName { get; set; }

        public DbContextModelBuilderOptions<TContext> GetOptions(DbContextModelBuilderOptions<TContext> existing)
        {
            var optionsProvided = existing != null;
            var opt = existing ?? BuildOptions();
            if (optionsProvided)
            {
                ConfigureOptions(opt);
            }

            return opt;
        }

        public void Dispose()
        {
            OnDisposed();
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        protected abstract void Configure(DbContextModelBuilderOptions<TContext> options);

        private DbContextModelBuilderOptions<TContext> BuildOptions()
        {
            if (defaultOptions != null)
            {
                return defaultOptions;
            }

            defaultOptions = new DbContextModelBuilderOptions<TContext>();
            ConfigureOptions(defaultOptions);
            return defaultOptions;
        }

        private void ConfigureOptions(DbContextModelBuilderOptions<TContext> options)
        {
            Configure(options);
            innerBuilders.ForEach(n => n.ConfigureOptions(options));
        }

        public event EventHandler Disposed;

        public DbContextModelBuilderOptions<TContext> Value => GetOptions(null);
    }
}
