using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ITVComponents.DataAccess.Remote.RemoteInterface;
using ITVComponents.Helpers;
using System.Text.Json.Serialization;
using ITVComponents.Logging;

namespace ITVComponents.DataAccess.Remote.ProxyObjects
{
    /// <summary>
    /// A command parameter that lives on the server and is used through a proxy. Serialized through its fields - not
    /// through <see cref="Value"/>, whose getter asks the server once the parameter is connected.
    /// </summary>
    [ITVComponents.Json.TransferByValue]
    public class RemoteDataParameter:IDbDataParameter
    {
        [JsonInclude]
        private long objectId;

        [NonSerialized]
        private IRemoteProxyDefinition connection;

        [JsonInclude]
        private object value;

        [JsonInclude]
        private bool isNullable;
        [JsonInclude]
        private DbType dbType;
        [JsonInclude]
        private ParameterDirection direction;
        [JsonInclude]
        private string parameterName;
        [JsonInclude]
        private string sourceColumn;
        [JsonInclude]
        private DataRowVersion sourceVersion;
        [JsonInclude]
        private byte precision;
        [JsonInclude]
        private byte scale;
        [JsonInclude]
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
        public bool IsNullable { get { return isNullable; } }
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
