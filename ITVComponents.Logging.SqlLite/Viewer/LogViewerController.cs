using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.SqLite;
using ITVComponents.Plugins;
using ITVComponents.Plugins.SelfRegistration;
using ITVComponents.UserInterface;
using ITVComponents.UserInterface.WindowExtensions;

namespace ITVComponents.Logging.SqlLite.Viewer
{
    public class LogViewerController : IUserInterface, IKeyHandler, INotifyPropertyChanged, IDeferredInit
    {
        private string logPath;

        private string searchFilter;

        private SqLogViewer gui;

        private string currentLog;

        private FilterSettings filterSettings;

        private IDbWrapper currentLogWrapper;

        public string UniqueName { get; set; }

        public bool HandleKeyDown { get; } = false;
        public bool HandleKeyUp { get; } = false;

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization => false;

        public ComboBoxItem SelectedLog
        {
            set
            {
                var raise = value.Tag as string != currentLog;
                currentLog = value.Tag as string;
                
                if (raise)
                {
                    currentLogWrapper?.Dispose();
                    currentLogWrapper = null;
                    OnPropertyChanged(nameof(SelectedLog));
                    OnPropertyChanged(nameof(LogData));
                }
            }
        }

        public LogViewerController(string logPath, string searchFilter)
        {
            this.logPath = logPath;
            this.searchFilter = searchFilter;
        }

        public Control GetUi()
        {
            if (gui == null)
            {
                gui = new SqLogViewer(this);
            }

            return gui;
        }
        public bool KeyStroke(KeyEventArgs keyEventArgs)
        {
            return false;
        }

        public ComboBoxItem[] LogFiles { get; private set; }

        public IEnumerable<LogDataModel> LogData
        {
            get { return ReadLogData(); }
        }

        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    RefreshLogs();
                    Init();
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        public void Dispose()
        {
            OnDisposed();
        }

        public void RefreshLogs()
        {
            DirectoryInfo inf = new DirectoryInfo(logPath);
            this.LogFiles = (from t in inf.GetFiles(searchFilter)
                select new ComboBoxItem { Tag = t.FullName, Content = t.Name }).ToArray();
            OnPropertyChanged(nameof(LogFiles));
        }

        public void RefreshLog()
        {
            OnPropertyChanged(nameof(LogData));
        }

        public void ApplyFilter(FilterSettings filterSettings)
        {
            this.filterSettings = filterSettings;
            OnPropertyChanged(nameof(LogData));
        }

        /// <summary>
        /// Runs Initializations on derived objects
        /// </summary>
        protected virtual void Init()
        {
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private IEnumerable<LogDataModel> ReadLogData()
        {
            IDbWrapper logWrapper = OpenLog();
            if (logWrapper == null)
            {
                return null;
            }

            return logWrapper.GetResults<LogDataModel>($"Select * from Log {filterSettings} order by EventTime",filterSettings?.GetArguments(logWrapper)??new IDbDataParameter[]{});
        }

        private IDbWrapper OpenLog()
        {
            if (currentLogWrapper == null && currentLog != null)
            {
                currentLogWrapper = new SqLiteWrapper(currentLog);
            }

            return currentLogWrapper;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler Disposed;
    }
}
