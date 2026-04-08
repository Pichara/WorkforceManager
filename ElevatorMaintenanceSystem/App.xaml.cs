using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.ViewModels;
using ElevatorMaintenanceSystem.Views;

namespace ElevatorMaintenanceSystem;

public partial class App : Application
{
    private IHost? _host;
    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Build the host
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.AddConfiguration();
                    services.AddSerilogLogging();
                    services.AddMongoDb();
                    services.AddRepositories();
                    services.AddTransient<ElevatorManagementViewModel>();
                    services.AddTransient<WorkerManagementViewModel>();
                    services.AddTransient<TicketManagementViewModel>();
                    services.AddTransient<MapViewModel>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            ServiceProvider = _host.Services;

            // Show the shell immediately so startup failures are visible to the user.
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            // Start hosted services (e.g., DatabaseInitializer) in the background.
            _ = StartHostAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Application startup failed before the main window could open.\n\n{ex.Message}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private async Task StartHostAsync()
    {
        if (_host == null)
        {
            return;
        }

        try
        {
            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Hosted services failed to start");

            MessageBox.Show(
                "The app opened, but backend startup failed. Check MongoDB and appsettings connection values.\n\n" +
                $"Error: {ex.Message}",
                "Startup Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
    }
}
