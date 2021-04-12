using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ITVComponents.InterProcessCommunication.ManagementExtensions.Scheduling;
using ITVComponents.UserInterface;

namespace ITVComponents.IPC.Management.Configuration.UI.SchedulePushUi
{
    public class TaskPageController : IUserInterface
    {
        /// <summary>
        /// the client that is used to communicate with the target service
        /// </summary>
        private ISchedulingManager client;

        /// <summary>
        /// the name of the target scheduler
        /// </summary>
        private string schedulerName;

        /// <summary>
        /// holds a list of available tasks for the remote scheduler
        /// </summary>
        private ObservableCollection<ScheduledTaskDescription>  tasks = new ObservableCollection<ScheduledTaskDescription>();

        private TaskPage gui;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a list of all available tasks
        /// </summary>
        public ReadOnlyObservableCollection<ScheduledTaskDescription> Tasks { get; private set; } 

        /// <summary>
        /// Initializes a new instance of the PushServiceTasks class
        /// </summary>
        /// <param name="client">the client object that is used to request data from the service</param>
        /// <param name="schedulerName">the name of the target scheduler</param>
        public TaskPageController(ISchedulingManager client, string schedulerName)
        {
            this.schedulerName = schedulerName;
            this.client = client;
            Tasks = new ReadOnlyObservableCollection<ScheduledTaskDescription>(tasks);
            Refresh();
        }

        /// <summary>
        /// Gets the default UI of this UI - Element
        /// </summary>
        public Control GetUi()
        {
            if (gui == null)
            {
                gui = new TaskPage(this);
            }

            return gui;
        }

        /// <summary>
        /// Refreshes the display list
        /// </summary>
        public void Refresh()
        {
            tasks.Clear();
            var localTasks = client.GetScheduledTasks(schedulerName);
            if (localTasks != null)
            {
                foreach (var task in localTasks)
                {
                    tasks.Add(task);
                }
            }
        }

        /// <summary>
        /// Pushes the selected task 
        /// </summary>
        /// <param name="requestId">the id of the task that must be pushed</param>
        public void Push(string requestId)
        {
            bool ok = client.PushRequest(schedulerName, requestId);
            if (ok)
            {
                MessageBox.Show("Task successfully started");
            }
            else
            {
                MessageBox.Show("Unable to push task!");
            }

            Refresh();
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
