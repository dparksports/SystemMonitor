using System.Configuration;
using System.Data;
using System.Windows;

namespace DeviceMonitorCS;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"Critical Error: {args.ExceptionObject}", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Error: {args.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

