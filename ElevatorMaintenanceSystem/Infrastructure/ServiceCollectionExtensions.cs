using ElevatorMaintenanceSystem.Data;
using ElevatorMaintenanceSystem.Models;
using ElevatorMaintenanceSystem.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization;
using Serilog;

namespace ElevatorMaintenanceSystem.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add configuration from appsettings.json
    /// </summary>
    public static IServiceCollection AddConfiguration(this IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Bind strongly-typed settings
        services.Configure<MongoDbSettings>(
            configuration.GetSection(MongoDbSettings.SectionName));

        // Register settings as a singleton for easy injection
        var mongoSettings = configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>()
            ?? new MongoDbSettings();
        services.AddSingleton(mongoSettings);

        return services;
    }

    /// <summary>
    /// Configure Serilog logging
    /// </summary>
    public static IServiceCollection AddSerilogLogging(this IServiceCollection services)
    {
        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog());

        return services;
    }

    /// <summary>
    /// Add MongoDB services
    /// </summary>
    public static IServiceCollection AddMongoDb(this IServiceCollection services)
    {
        services.AddSingleton<IMongoDbContext, MongoDbContext>();
        services.AddHostedService<DatabaseInitializer>();

        // Register MongoDB class mappings
        RegisterMongoClassMappings();

        return services;
    }

    /// <summary>
    /// Add generic repositories
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Register generic repository as scoped
        services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));

        services.AddScoped<IElevatorRepository, ElevatorRepository>();
        services.AddScoped<IWorkerRepository, WorkerRepository>();
        services.AddScoped<IElevatorService, ElevatorService>();
        services.AddScoped<IWorkerService, WorkerService>();
        services.AddSingleton<GpsCoordinateValidator>();

        return services;
    }

    /// <summary>
    /// Register MongoDB class mappings for document types
    /// </summary>
    private static void RegisterMongoClassMappings()
    {
        // BaseDocument mapping is handled automatically by the driver
        // Additional mappings can be added here for complex types
        if (!BsonClassMap.IsClassMapRegistered(typeof(BaseDocument)))
        {
            BsonClassMap.RegisterClassMap<BaseDocument>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }
}
