namespace ITVComponents.EFRepo.DynamicData
{
    public class TableDiff
    {
        public string ColumnName { get;set; }

        public TableColumnDefinition Table1Def { get; set; }

        public TableColumnDefinition Table2Def{get; set; }
    }
}
