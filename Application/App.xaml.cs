using System.Windows;
using System.Threading;
using System.Windows.Threading;

namespace ToolKitV
{
    public partial class App : Application
    {
        protected Mutex? Mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Single-instance guard — prevent running TGToolKit more than once at a time.
            Mutex = new Mutex(true, "TGToolKit_SingleInstance");

            if (!Mutex.WaitOne(0, false))
            {
                MessageBox.Show(
                    "TGToolKit is already running.",
                    "TGToolKit",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            ShutdownMode = ShutdownMode.OnLastWindowClose;
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Mutex?.ReleaseMutex();
            Mutex?.Dispose();
            base.OnExit(e);
        }

        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "An unexpected error occurred:\n\n" + e.Exception.Message
                + "\n\nCheck log.txt in the application folder for details.",
                "TGToolKit — Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }
    }
}
