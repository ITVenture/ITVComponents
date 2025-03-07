using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ITVComponents.DataAccess.Remote.RemoteInterface;
using ITVComponents.Helpers;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;

namespace ITVComponents.DataAccess.Remote.ProxyObjects
{
    public class RemoteDataParameter:IDbDataParameter, IManualSerializer
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

        public RemoteDataParameter()
        {
        }

        ~RemoteDataParameter()
        {
            if (connection != null)
            {
                try
                {
                    connection.DestroyParameter(objectId);
                    connection = null;
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Unable to destroy parameter: {ex.OutlineException()}", LogSeverity.Error);
                }
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
            Data.Add(ManualSerializationData.FromValue(nameof(objectId), objectId));
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
            objectId = Data.GetDeserializedValue<long>(nameof(objectId));
        }
    }
}
