using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OnFlight.App.ViewModels;
using OnFlight.Contracts.Sync;
using OnFlight.Core.Data;
using OnFlight.Core.Data.Repositories;
using OnFlight.Core.Services;
using OnFlight.Core.Settings;
using Serilog;
using Serilog.Events;

namespace OnFlight.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OnFlight", "logs", "onflight-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug()
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        var settingsService = new SettingsService();

        ApplyThemeFromSettings(settingsService.Current);

        var dbPath = settingsService.GetDatabasePath();
        var connectionString = $"Data Source={dbPath}";

        DapperConfig.RegisterTypeHandlers();

        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        services.AddSingleton<ISettingsService>(settingsService);
        services.AddSingleton(new DbConnectionFactory(connectionString));
        services.AddSingleton(sp => new DatabaseInitializer(connectionString,
            sp.GetRequiredService<ILogger<DatabaseInitializer>>()));
        services.AddSingleton<ITodoListRepository, TodoListRepository>();
        services.AddSingleton<ITodoItemRepository, TodoItemRepository>();
        services.AddSingleton<IOperationLogRepository, OperationLogRepository>();
        services.AddSingleton<IRunningInstanceRepository, RunningInstanceRepository>();

        services.AddSingleton<ITodoService, TodoService>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<ISyncProvider, NoOpSyncProvider>();
        services.AddSingleton<IRunningTaskService, RunningTaskService>();

        services.AddSingleton<TaskEventBus>();
        services.AddSingleton<RunningTaskManager>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<FloatingViewModel>();
        services.AddTransient<SettingsViewModel>();

        Services = services.BuildServiceProvider();

        Services.GetRequiredService<DatabaseInitializer>().Initialize();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow();
            desktop.ShutdownRequested += (_, _) => Log.CloseAndFlush();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ApplyThemeFromSettings(AppSettings settings)
    {
        RequestedThemeVariant = settings.Appearance.ThemeMode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
