namespace ITVComponents.EFRepo.DynamicData
{
    public class DynamicTableSort
    {
        public string ColumnName { get; set; }

        public SortOrder SortOrder { get; set; }
    }

    public enum SortOrder
    {
        Asc,
        Desc
    }
}
