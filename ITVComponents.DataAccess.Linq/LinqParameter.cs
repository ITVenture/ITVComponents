using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ITVComponents.Helpers;
namespace ITVComponents.DataAccess.Linq
{
    [Serializable]
    public class LinqParameter:IDbDataParameter, ISerializable
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

        public LinqParameter(SerializationInfo info, StreamingContext context)
        {
            dbType = (DbType) info.GetValue(nameof(dbType),typeof(DbType));
            direction= (ParameterDirection)info.GetValue(nameof(direction),typeof(ParameterDirection));
            isNullable = (bool)info.GetValue(nameof(isNullable),typeof(bool));
            parameterName=(string)info.GetValue(nameof(parameterName),typeof(string));
            sourceColumn = (string)info.GetValue(nameof(sourceColumn),typeof(string));
            sourceVersion = (DataRowVersion) info.GetValue(nameof(sourceVersion), typeof(DataRowVersion));
            value= info.GetObject(nameof(value));
            precision=(byte)info.GetValue(nameof(precision),typeof(byte));
            scale = (byte)info.GetValue(nameof(scale),typeof(byte));
            size = (int) info.GetValue(nameof(size), typeof(int));
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

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(dbType), dbType);
            info.AddValue(nameof(direction),direction);
            info.AddValue(nameof(isNullable),isNullable);
            info.AddValue(nameof(parameterName),parameterName);
            info.AddValue(nameof(sourceColumn),sourceColumn);
            info.AddValue(nameof(sourceVersion),sourceVersion);
            info.AddValue(nameof(value),value);
            info.AddValue(nameof(precision),precision);
            info.AddValue(nameof(scale),scale);
            info.AddValue(nameof(size),size);
        }
    }
}
