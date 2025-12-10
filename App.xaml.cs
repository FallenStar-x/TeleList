using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace TeleList
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Register code pages encoding provider for Windows-1252 and other encodings
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Global exception handlers to prevent crashes
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            LogError("Unhandled exception", exception);

            if (e.IsTerminating)
            {
                MessageBox.Show(
                    $"A critical error occurred:\n\n{exception?.Message ?? "Unknown error"}\n\nThe application will close.",
                    "Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError("UI thread exception", e.Exception);

            MessageBox.Show(
                $"An error occurred:\n\n{e.Exception.Message}\n\nThe application will attempt to continue.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            e.Handled = true; // Prevent crash, allow app to continue
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogError("Background task exception", e.Exception);
            e.SetObserved(); // Prevent crash from background tasks
        }

        private static void LogError(string context, Exception? exception)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {exception?.ToString() ?? "Unknown error"}";
                System.Diagnostics.Debug.WriteLine(logMessage);

                // Optionally write to a log file
                var logPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "telelist_errors.log");
                System.IO.File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }
            catch
            {
                // Ignore logging failures
            }
        }
    }
}
