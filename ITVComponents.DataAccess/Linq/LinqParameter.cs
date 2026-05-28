using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ITVComponents.Helpers;
using ITVComponents.Json.Contracts;

namespace ITVComponents.DataAccess.Linq
{
    public class LinqParameter:IDbDataParameter, IManualSerializer
    {
        private DbType dbType;
        private ParameterDirection direction;
        private bool isNullable;
        private string parameterName;
        private string sourceColumn;
        private DataRowVersion sourceVersion;
        private object value;
        private byte precision;
        private byte scale;
        private int size;

        public LinqParameter()
        {
        }

        public LinqParameter(bool nullable)
        {
            isNullable = nullable;
        }

        public DbType DbType
        {
            get { return dbType; }
            set { dbType = value; }
        }

        public ParameterDirection Direction
        {
            get { return direction; }
            set { direction = value; }
        }

        public bool IsNullable
        {
            get { return isNullable; }
        }

        public string ParameterName
        {
            get { return parameterName; }
            set { parameterName = value; }
        }

        public string SourceColumn
        {
            get { return sourceColumn; }
            set { sourceColumn = value; }
        }

        public DataRowVersion SourceVersion
        {
            get { return sourceVersion; }
            set { sourceVersion = value; }
        }

        public object Value
        {
            get { return value; }
            set { this.value = value; }
        }

        public byte Precision
        {
            get { return precision; }
            set { precision = value; }
        }

        public byte Scale
        {
            get { return scale; }
            set { scale = value; }
        }

        public int Size
        {
            get { return size; }
            set { size = value; }
        }

        public IList<ManualSerializationData> Data { get; set; }
        public void GetObjectData()
        {
            Data.Add(ManualSerializationData.FromValue(nameof(dbType), dbType));
            Data.Add(ManualSerializationData.FromValue(nameof(direction), direction));
            Data.Add(ManualSerializationData.FromValue(nameof(isNullable), isNullable));
            Data.Add(ManualSerializationData.FromValue(nameof(parameterName), parameterName));
            Data.Add(ManualSerializationData.FromValue(nameof(sourceColumn), sourceColumn));
            Data.Add(ManualSerializationData.FromValue(nameof(sourceVersion), sourceVersion));
            Data.Add(ManualSerializationData.FromValue(nameof(value), value));
            Data.Add(ManualSerializationData.FromValue(nameof(precision), precision));
            Data.Add(ManualSerializationData.FromValue(nameof(scale), scale));
            Data.Add(ManualSerializationData.FromValue(nameof(size), size));
        }

        public void ApplyObjectData()
        {
            dbType = Data.GetDeserializedValue<DbType>(nameof(dbType));
            direction = Data.GetDeserializedValue<ParameterDirection>(nameof(direction));
            isNullable = Data.GetDeserializedValue<bool>(nameof(isNullable));
            parameterName = Data.GetDeserializedValue<string>(nameof(parameterName));
            sourceColumn = Data.GetDeserializedValue<string>(nameof(sourceColumn));
            sourceVersion = Data.GetDeserializedValue<DataRowVersion>(nameof(sourceVersion));
            value = Data.GetDeserializedValue<object>(nameof(value));
            precision = Data.GetDeserializedValue<byte>(nameof(precision));
            scale = Data.GetDeserializedValue<byte>(nameof(scale));
            size = Data.GetDeserializedValue<int>(nameof(size));
        }
    }
}
