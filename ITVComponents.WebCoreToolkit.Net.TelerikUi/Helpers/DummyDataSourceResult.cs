using System.Collections;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers
{
    public class DummyDataSourceResult
    {
        public IEnumerable Data { get; set; }
        public int Total { get; set; }
        public object[] AggregateResults { get;  } = new object[0];
        public object Errors { get; } = null;
    }
}
