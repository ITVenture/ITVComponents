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

namespace ITVComponents.Logging.SqlLite.Viewer
{
    /// <summary>
    /// Interaktionslogik für SqLogViewer.xaml
    /// </summary>
    public partial class SqLogViewer : UserControl
    {
        public DataGrid LogGrid
        {
            get { return logGrid; }
        }

        public SqLogViewer(LogViewerController controller)
        {
            this.Controller = controller;
            InitializeComponent();
        }

        public LogViewerController Controller { get; private set; }

        private void RefreshLog(object sender, RoutedEventArgs e)
        {
            Controller.RefreshLog();
        }

        private void FilterData(object sender, RoutedEventArgs e)
        {
            var dialog = new FilterWindow();
            if (dialog.ShowDialog() ?? false)
            {
                var filterSettings = dialog.FilterSettings;
                Controller.ApplyFilter(filterSettings);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Controller.ApplyFilter(null);
        }
    }
}
