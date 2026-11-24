using System.Data;
using System.Text.Json.Serialization;

namespace ITVComponents.DataAccess.Linq
{
    /// <summary>
    /// A command parameter of the linq data connector. Serialized through its fields; <see cref="Value"/> is an open place
    /// and keeps its runtime type (<c>SerializationTypingMode.EnvelopePolymorphism</c>).
    /// </summary>
    [ITVComponents.Json.TransferByValue]
    public class LinqParameter:IDbDataParameter
    {
        [JsonInclude]
        private DbType dbType;

        [JsonInclude]
        private ParameterDirection direction;

        [JsonInclude]
        private bool isNullable;

        [JsonInclude]
        private string parameterName;

        [JsonInclude]
        private string sourceColumn;

        [JsonInclude]
        private DataRowVersion sourceVersion;

        [JsonInclude]
        private object value;

        [JsonInclude]
        private byte precision;

        [JsonInclude]
        private byte scale;

        [JsonInclude]
        private int size;

        public LinqParameter()
        {
        }

        public LinqParameter(bool nullable)
        {
            isNullable = nullable;
        }

        [JsonIgnore]
        public DbType DbType
        {
            get { return dbType; }
            set { dbType = value; }
        }

        [JsonIgnore]
        public ParameterDirection Direction
        {
            get { return direction; }
            set { direction = value; }
        }

        [JsonIgnore]
        public bool IsNullable
        {
            get { return isNullable; }
        }

        [JsonIgnore]
        public string ParameterName
        {
            get { return parameterName; }
            set { parameterName = value; }
        }

        [JsonIgnore]
        public string SourceColumn
        {
            get { return sourceColumn; }
            set { sourceColumn = value; }
        }

        [JsonIgnore]
        public DataRowVersion SourceVersion
        {
            get { return sourceVersion; }
            set { sourceVersion = value; }
        }

        [JsonIgnore]
        public object Value
        {
            get { return value; }
            set { this.value = value; }
        }

        [JsonIgnore]
        public byte Precision
        {
            get { return precision; }
            set { precision = value; }
        }

        [JsonIgnore]
        public byte Scale
        {
            get { return scale; }
            set { scale = value; }
        }

        [JsonIgnore]
        public int Size
        {
            get { return size; }
            set { size = value; }
        }
    }
}
