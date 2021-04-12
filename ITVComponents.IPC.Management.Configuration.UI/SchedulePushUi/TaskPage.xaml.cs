using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ITVComponents.InterProcessCommunication.ManagementExtensions.Scheduling;

namespace ITVComponents.IPC.Management.Configuration.UI.SchedulePushUi
{
    /// <summary>
    /// Interaktionslogik für TaskPage.xaml
    /// </summary>
    public partial class TaskPage : UserControl
    {
        /// <summary>
        /// Gets the Controller that is used to provide Record data
        /// </summary>
        public TaskPageController Controller { get; private set; }

        public TaskPage(TaskPageController controller)
        {
            Controller = controller;
            InitializeComponent();

            // LogEnvironment.LogEvent(string.Format("Source is {0}",dingsbums.ItemsSource.GetType()), LogSeverity.Report);
        }

        private void RefreshNow(object sender, RoutedEventArgs e)
        {
            Controller.Refresh();
        }

        private void PushSelected(object sender, RoutedEventArgs e)
        {
            if (TaskGrid.SelectedItem == null)
            {
                MessageBox.Show("Es wurde nichts ausgewählt");
                return;
            }

            ScheduledTaskDescription desc = (ScheduledTaskDescription)TaskGrid.SelectedItem;
            Controller.Push(desc.RequestId);
        }
    }
}
