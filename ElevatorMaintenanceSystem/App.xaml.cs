using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Views;
using ElevatorMaintenanceSystem.ViewModels;

namespace ElevatorMaintenanceSystem;

public partial class App : Application
{
    private IHost? _host;
    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        ServiceProvider = _host.Services;

        // Hosted services (DatabaseInitializer) will start automatically
        _host.Start();

        // Show main window
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
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
