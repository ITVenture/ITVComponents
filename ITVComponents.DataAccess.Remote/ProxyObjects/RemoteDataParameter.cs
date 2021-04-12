using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ITVComponents.DataAccess.Remote.RemoteInterface;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.DataAccess.Remote.ProxyObjects
{
    [Serializable]
    public class RemoteDataParameter:IDbDataParameter, ISerializable
    {
        private long objectId;

        [NonSerialized]
        private IRemoteProxyDefinition connection;

        private object value;

        private bool isNullable;
        private DbType dbType;
        private ParameterDirection direction;
        private string parameterName;
        private string sourceColumn;
        private DataRowVersion sourceVersion;
        private byte precision;
        private byte scale;
        private int size;

        public RemoteDataParameter(IDbDataParameter src, long objectId)
        {
            this.objectId = objectId;
            DbType = src.DbType;
            LogEnvironment.LogDebugEvent(null, DbType.GetType().FullName, (int) LogSeverity.Report, null);
            Direction = src.Direction;
            LogEnvironment.LogDebugEvent(null, direction.GetType().FullName, (int) LogSeverity.Report, null);
            isNullable = src.IsNullable;
            ParameterName = src.ParameterName;
            SourceColumn = src.SourceColumn;
            sourceVersion = src.SourceVersion;
            LogEnvironment.LogDebugEvent(null, sourceVersion.GetType().FullName, (int) LogSeverity.Report, null);
            value = src.Value;
            Precision = src.Precision;
            Scale = src.Scale;
            Size = src.Size;
        }

        public RemoteDataParameter(SerializationInfo info, StreamingContext context)
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
            objectId = (long) info.GetValue(nameof(objectId), typeof(long));
        }

        ~RemoteDataParameter()
        {
            if (connection != null)
            {
                connection.DestroyParameter(objectId);
                connection = null;
            }
        }

        internal long ObjectId { get { return objectId; } }

        internal void ClientInit(IRemoteProxyDefinition connection)
        {
            this.connection = connection;
        }

        internal void ApplyTo(IDbDataParameter target)
        {
            target.DbType = DbType;
            target.Direction = Direction;
            target.ParameterName = ParameterName;
            target.SourceColumn = SourceColumn;
            target.Value = value;
            target.Precision = Precision;
            target.Scale = Scale;
            target.Size = Size;
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

        public bool IsNullable { get { return isNullable; } }
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
            get
            {
                if (connection != null)
                {
                    return connection.GetParameterValue(objectId);
                }
                return value;
            }
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
            info.AddValue(nameof(objectId), objectId);
        }
    }
}
