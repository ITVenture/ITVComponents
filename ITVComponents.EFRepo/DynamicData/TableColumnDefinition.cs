using System;

namespace ITVComponents.EFRepo.DynamicData
{
    public class TableColumnDefinition
    {
        public string ColumnName { get; set; }
        
        public string DataType{get; set; }
        
        public DynamicDataColumnType Type {get;set;}
        
        public int? DataLength { get; set; }
        
        public bool Nullable{get;set;}
        
        public int Position { get;set; }
        
        public bool IsIdentity{get; set; }
        
        public bool IsForeignKey { get;set; }
        
        public string RefTable { get; set; }
        
        public string RefColumn { get; set; }
        
        public bool HasIndex{get;set;}
        
        public bool IsPrimaryKey { get; set; }
        
        public bool IsUniqueKey{get; set; }
        
        public bool HasReferences{get; set; }
    }
}
