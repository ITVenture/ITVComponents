using System;

namespace ITVComponents.EFRepo.DynamicData
{
    public class DynamicQueryCallbackProvider
    {
        public TableColumnResolveCallback FQColumnQuery { get; set; }
        public string CustomQuerySelection { get; set; }
        public string CustomQueryTablePart { get; set; }
    }
}
