using ITVComponents.EFRepo.DataSync.Models;

namespace ITVComponents.EFRepo.DataSync
{
    /// <summary>
    /// Change-building helpers handed to an <see cref="IConfigExtension"/> during compare. Implemented by the
    /// hosting configuration-handler, so the foreign-key Script-Linq expressions are bound to the host's concrete
    /// context type and plugin unique-name — an extension is not itself a <c>ConfigurationHandlerBase</c> and
    /// therefore cannot build those expressions on its own.
    /// </summary>
    public interface IConfigChangeContext
    {
        /// <summary>Creates a per-property change detail (IST = <paramref name="currentValue"/>, SOLL = <paramref name="value"/>).</summary>
        ChangeDetail MakeDetail(string columnName, string value, string valueExpression = null, string currentValue = null, bool multiline = false, bool apply = true);

        /// <summary>
        /// Builds a Script-Linq assignment that resolves a foreign key on apply by looking up
        /// <paramref name="sourceEntity"/> via <paramref name="filterProperty"/> (bound to the host context type).
        /// </summary>
        string MakeLinqAssign(string targetProperty, string sourceEntity, string filterProperty, string additionalWhere = null, bool ignoreFail = false, string managedFilterType = null, string scriptedFilterType = null);

        /// <summary>Builds a Script-Linq query that resolves a foreign-key value (see <see cref="MakeLinqAssign"/>).</summary>
        string MakeLinqQuery(string sourceEntity, string filterProperty, string additionalWhere = null, bool ignoreFail = false, string managedFilterType = null, string scriptedFilterType = null, string filterValueVariable = "Value");
    }
}
