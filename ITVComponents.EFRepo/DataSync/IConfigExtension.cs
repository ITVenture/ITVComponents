using System.Collections.Generic;
using ITVComponents.EFRepo.DataSync.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DataSync
{
    /// <summary>
    /// Contributes an additional section to the system-configuration export/diff. A feature library implements
    /// this once per section and registers it via <c>AddSystemConfigExtension&lt;TMarkup&gt;()</c>. Apply is not
    /// part of the contract: <see cref="Compare"/> emits standard <see cref="Change"/> objects that the existing
    /// generic apply engine (SimpleDataApplyer) persists — the extension only describes (IST) and diffs.
    /// </summary>
    public interface IConfigExtension
    {
        /// <summary>Stable section key; equals the registered polymorphism discriminator.</summary>
        string SectionKey { get; }

        /// <summary>Captures the current (IST) state of this section as its typed markup, or null to contribute nothing.</summary>
        ConfigExtensionMarkup Describe(DbContext db);

        /// <summary>
        /// Compares current (IST) vs uploaded (SOLL) and yields <see cref="Change"/> objects for the generic
        /// applyer. <paramref name="current"/> may be null (nothing present yet on this system). Use
        /// <paramref name="changes"/> to build details and foreign-key-resolving assignments.
        /// </summary>
        IEnumerable<Change> Compare(DbContext db, ConfigExtensionMarkup current, ConfigExtensionMarkup uploaded, IConfigChangeContext changes);
    }
}
