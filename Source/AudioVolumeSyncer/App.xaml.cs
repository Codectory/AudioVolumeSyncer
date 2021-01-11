using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AudioVolumeSyncer
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    ///

    public partial class App : Application
    {
        static Mutex mutex = new Mutex(true, "{0ABB7A84-A006-41C5-979E-B5EDA3CB67E5}");
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                mutex.ReleaseMutex();

            }
            else
            {
                MessageBox.Show(Locale_Texts.AlreadyRunning);
                Application.Current.Shutdown();
            }
        }
        private void Application_Exit(object sender, ExitEventArgs e)
        {

        }
    }
}
