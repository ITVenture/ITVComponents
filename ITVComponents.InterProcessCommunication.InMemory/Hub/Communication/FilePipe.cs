using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Communication
{
    /// <summary>
    /// A File-Pipe that allows unidirectional communication between a server and n clients
    /// </summary>
    public class FilePipe:IDisposable
    {
        /// <summary>
        /// The buffersize that is used for the communication
        /// </summary>
        private int bufferSize = 4096;

        /// <summary>
        /// Shortcut array that represents the integer value of 0
        /// </summary>
        private byte[] zero = new byte[] { 0, 0, 0, 0 };

        /// <summary>
        /// a buffer that represents an integer number
        /// </summary>
        private byte[] lenBuf = new byte[4];

        /// <summary>
        /// the memory-mapped file that is used for communication
        /// </summary>
        private MemoryMappedFile fileHandle;

        /// <summary>
        /// the global memory-mapped file that is used for communication
        /// </summary>
        private GlobalMemoryMappedFile globalHandle;

        /// <summary>
        /// an InterProcessWaithandle that synchronizes the write-actions between clients
        /// </summary>
        private InterProcessWaitHandle mux;

        /// <summary>
        /// an InterProcessWaitHandle that synchronizes the write-actions between the active client and the server
        /// </summary>
        private InterProcessWaitHandle cmux;

        /// <summary>
        /// A Stream-object that allows read/write actions on the MemorymappedFile object
        /// </summary>
        private UnmanagedMemoryStream mvs;

        /// <summary>
        /// The read-buffer that is used by the server-object to process incoming data from the clients
        /// </summary>
        private byte[] readBuffer;

        /// <summary>
        /// A buffer object that enables the pipe to send/receive objects that are larger than the read-buffer
        /// </summary>
        private List<byte> incomingData = new List<byte>();

        /// <summary>
        /// A dummy WaitHandle that is always closed and enables this pipe to perform waiting without caling thread.sleep
        /// </summary>
        private ManualResetEvent forEverClosed;

        /// <summary>
        /// A Task-object that represents the read-method which runs for ever
        /// </summary>
        private Task listener;

        /// <summary>
        /// A Cancellation-token source that helps ending the read-thread
        /// </summary>
        private CancellationTokenSource waiter;

        /// <summary>
        /// the Cancellationtoken of the waiter-source
        /// </summary>
        private CancellationToken waitToken;

        /// <summary>
        /// Indicates whether the thread is currently listening
        /// </summary>
        private bool listening = false;

        /// <summary>
        /// syncs listening startup
        /// </summary>
        private object listenLock = new object();

        /// <summary>
        /// Initializes a new instance of the FilePipe class
        /// </summary>
        /// <param name="name">the name of the target memory-mapped file</param>
        public FilePipe(string name)
        {
            if (!name.StartsWith(@"Global\"))
            {
                fileHandle = MemoryMappedFile.CreateOrOpen(name, bufferSize, MemoryMappedFileAccess.ReadWrite);
                mux = new InterProcessWaitHandle($"{name}_mx");
                cmux = new InterProcessWaitHandle($"{name}_cmx");
                mvs = fileHandle.CreateViewStream();
                forEverClosed = new ManualResetEvent(false);
            }
            else
            {
                globalHandle = new GlobalMemoryMappedFile(name, bufferSize);//MemoryMappedFile.CreateOrOpen(name, bufferSize, MemoryMappedFileAccess.ReadWrite);
                mux = new InterProcessWaitHandle($"{name}_mx");
                cmux = new InterProcessWaitHandle($"{name}_cmx");
                mvs = globalHandle.ViewStream;
                forEverClosed = new ManualResetEvent(false);
                IsGlobal = true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a global pipe
        /// </summary>
        public bool IsGlobal { get; }

        /// <summary>
        /// Writes data into the memory-mapped file and blocks until the data was processed by the server or a timeout occurs
        /// </summary>
        /// <param name="data">the data to transmit to the target-endpoint</param>
        public void Write(string data)
        {
            var awaiter = WriteAsync(data).ConfigureAwait(false).GetAwaiter();
            awaiter.GetResult();
        }

        /// <summary>
        /// Writes data into the memory-mapped file and blocks until the data was processed by the server or a timeout occurs
        /// </summary>
        /// <param name="data">the data to transmit to the target-endpoint</param>
        public async Task WriteAsync(string data)
        {
            await WriteAsync(data, CancellationToken.None);
        }

        /// <summary>
        /// Writes data into the memory-mapped file and blocks until the data was processed by the server or a timeout occurs
        /// </summary>
        /// <param name="data">the data to transmit to the target-endpoint</param>
        /// <param name="cancellationToken">the cancellation token to control the inner async method executions</param>
        public async Task WriteAsync(string data, CancellationToken cancellationToken)
        {
            mux.WaitOne();
            LogEnvironment.LogDebugEvent("Starting Write...", LogSeverity.Report);
            try
            {

                var tmp = Encoding.UTF8.GetBytes(data);
                var len = tmp.Length;
                tmp = BitConverter.GetBytes(len).Concat(tmp).ToArray();
                var id = 0;
                var ln = 0;
                while (id < tmp.Length && !cancellationToken.IsCancellationRequested)
                {
                    ln = tmp.Length - id > bufferSize - sizeof(int) ? bufferSize - sizeof(int) : tmp.Length - id;
                    cmux.WaitOne();
                    try
                    {
                        var dat = BitConverter.GetBytes(ln).Concat(tmp.Skip(id).Take(ln)).ToArray();
                        mvs.Seek(0, SeekOrigin.Begin);
                        await mvs.WriteAsync(dat, 0, dat.Length, cancellationToken);
                        int ok = -1;
                        int attempts = 0;
                        while (ok != 0 && !cancellationToken.IsCancellationRequested)
                        {
                            cmux.WaitOne();
                            mvs.Seek(0, SeekOrigin.Begin);
                            await mvs.ReadAsync(lenBuf, 0, 4, cancellationToken);
                            ok = BitConverter.ToInt32(lenBuf, 0);
                            if (ok != 0)
                            {
                                attempts++;
                                if (attempts == 15)
                                {
                                    forEverClosed.WaitOne(600);
                                }
                                else
                                {
                                    forEverClosed.WaitOne(20);
                                }

                                if (attempts > 30)
                                {
                                    mvs.Seek(0, SeekOrigin.Begin);
                                    await mvs.WriteAsync(zero, 0, 4, cancellationToken);
                                    throw new CommunicationException("Nobody listening!");
                                }
                            }
                        }
                    }
                    finally
                    {
                        cmux.Pulse();
                    }

                    id += ln;
                }
            }
            finally
            {
                LogEnvironment.LogDebugEvent("Write done.", LogSeverity.Report);
                mux.Pulse();
            }
        }

        /// <summary>
        /// Starts listening to the MemoryMappedFile 
        /// </summary>
        public void Listen()
        {
            lock (listenLock)
            {
                if (!listening)
                {
                    readBuffer = new byte[bufferSize];
                    //listener = new Thread(ListenToStream);
                    listening = true;
                    waiter = new CancellationTokenSource();
                    waitToken = waiter.Token;
                    listener = Task.Run(ListenToStream, waitToken);
                }
            }
            //listener.Start();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            if (listening)
            {
                listening = false;
                waiter.Cancel();
                listener.GetAwaiter().GetResult();
                listener = null;
            }

            mvs?.Dispose();
            fileHandle?.Dispose();
            globalHandle?.Dispose();
            mux?.Dispose();
            forEverClosed?.Dispose();
            mvs = null;
            fileHandle = null;
            globalHandle = null;
            mux = null;
            forEverClosed = null;
        }

        /// <summary>
        /// Raises the DataReceived event
        /// </summary>
        /// <param name="e">an object containing the received data</param>
        protected virtual void OnDataReceived(IncomingDataEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }

        /// <summary>
        /// Listens synchronized to the data that is transmitted through the MemoryMappedFile object
        /// </summary>
        /// <returns>a task</returns>
        private async Task ListenToStream()
        {
            try
            {
                int tick = 0;
                cmux.WaitOne();
                while (listening && !waitToken.IsCancellationRequested)
                {
                    cmux.WaitOne();
                    mvs.Seek(0, SeekOrigin.Begin);
                    var tmp = await mvs.ReadAsync(readBuffer, 0, bufferSize, waitToken);
                    var ln = BitConverter.ToInt32(readBuffer, 0);
                    if (ln != 0 && tmp != 0)
                    {
                        LogEnvironment.LogDebugEvent($"Incoming message (length: {ln})", LogSeverity.Report);
                        incomingData.AddRange(readBuffer.Skip(4).Take(ln));
                        mvs.Seek(0, SeekOrigin.Begin);
                        await mvs.WriteAsync(zero, 0, 4, waitToken);
                        //cmux.Pulse();
                        cmux.WaitOne();
                        ProcessIncoming();
                        tick = 0;
                    }
                    else
                    {
                        tick++;
                        if (tick % 50 == 0)
                        {
                            tick = 0;
                            forEverClosed.WaitOne(500);
                        }
                        else
                        {
                            forEverClosed.WaitOne(20);
                        }
                    }
                }
            }
            finally
            {
                listening = false;
            }
        }

        /// <summary>
        /// Processes a complete data-block that was transferred through the memory-mapped file
        /// </summary>
        private void ProcessIncoming()
        {
            if (incomingData.Count >= 4)
            {
                incomingData.CopyTo(0, lenBuf, 0, 4);
                var expectedLen = BitConverter.ToInt32(lenBuf, 0) + 4;
                LogEnvironment.LogDebugEvent($"Expected length: {expectedLen}, effective length: {incomingData.Count}", LogSeverity.Report);
                if (incomingData.Count >= expectedLen && expectedLen > 4)
                {
                    var s = Encoding.UTF8.GetString(incomingData.Skip(4).Take(expectedLen - 4).ToArray());
                    incomingData.RemoveRange(0, expectedLen);
                    OnDataReceived(new IncomingDataEventArgs {Data = s});
                }
                else if (incomingData.Count >= expectedLen)
                {
                    incomingData.RemoveRange(0, expectedLen);
                }
            }
        }

        /// <summary>
        /// Is raised when a data-block was delivered through the MemoryMappedFile object
        /// </summary>
        public event EventHandler<IncomingDataEventArgs> DataReceived;
    }
}
