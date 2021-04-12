using System.Collections;
using System.Linq;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Creates a dummy DataSource result that is compatible to Kendo filter-requests
        /// </summary>
        /// <param name="source">the source for which to create a dummy-result</param>
        /// <returns>a DataSource Result that is compatible to kendo-datasource-requests</returns>
        public static DummyDataSourceResult ToDummyDataSourceResult(this IEnumerable source)
        {
            var retVal = new DummyDataSourceResult();
            var src = source.Cast<object>().ToArray();
            retVal.Data = src;
            retVal.Total = src.Length;
            return retVal;
        }
    }
}
