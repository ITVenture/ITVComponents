using System.Windows;

namespace ITVComponents.Logging.SqlLite.Viewer
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class FilterWindow : Window
    {

        public FilterWindow()
        {
            InitializeComponent();
        }

        public FilterSettings FilterSettings { get; } = new FilterSettings();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
