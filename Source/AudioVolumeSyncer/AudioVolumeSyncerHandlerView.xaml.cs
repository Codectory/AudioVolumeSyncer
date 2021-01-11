using CodectoryCore.UI.Wpf;
using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace AudioVolumeSyncer
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class AudioVolumeSyncerHandlerView : MainWindowBase
    {
        public AudioVolumeSyncerHandlerView()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Properties.Settings.Default.Width = this.Width;
            Properties.Settings.Default.Height = this.Height;
            Properties.Settings.Default.Save();
            this.Hide();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {

                // for .NET Core you need to add UseShellExecute = true
                // see https://docs.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                e.Handled = true;

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Width= Properties.Settings.Default.Width;
            this.Height = Properties.Settings.Default.Height;

        }
    }
}
