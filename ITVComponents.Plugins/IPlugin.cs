//-----------------------------------------------------------------------
// <copyright file="IPlugin.cs" company="IT-Venture GmbH">
//     2009 by IT-Venture GmbH
// </copyright>
//-----------------------------------------------------------------------
namespace ITVComponents.Plugins
{
    using System;

    /// <summary>
    /// Basic - Interface for plugins
    /// </summary>
    public interface IPlugin : IDisposable
    {
        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        event EventHandler Disposed;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        string UniqueName { get; set; }
    }
}