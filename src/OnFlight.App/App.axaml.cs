using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
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

    private TrayIcon? _trayIcon;
    private static bool _isQuitting;
    public static bool IsApplicationQuitting => _isQuitting;

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
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new Views.MainWindow();
            desktop.ShutdownRequested += (_, _) => Log.CloseAndFlush();
            EnsureTrayIcon();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon != null)
            return;

        WindowIcon? winIcon = null;
        try
        {
            var uri = new Uri("avares://OnFlight.App/Assets/tray.png");
            if (AssetLoader.Exists(uri))
                winIcon = new WindowIcon(AssetLoader.Open(uri));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Tray icon asset could not be loaded");
        }

        _trayIcon = new TrayIcon
        {
            Icon = winIcon,
            ToolTipText = "OnFlight",
            IsVisible = true,
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        var menu = new NativeMenu();
        var showMain = new NativeMenuItem("Show Main Window");
        showMain.Click += (_, _) => ShowMainWindow();
        var newFloat = new NativeMenuItem("New Floating Window");
        newFloat.Click += (_, _) => OpenNewFloatingWindow();
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => QuitApplication();
        menu.Items.Add(showMain);
        menu.Items.Add(newFloat);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);
        _trayIcon.Menu = menu;

        var icons = TrayIcon.GetIcons(this);
        if (icons == null)
        {
            icons = new TrayIcons();
            TrayIcon.SetIcons(this, icons);
        }

        icons.Add(_trayIcon);
    }

    public void ShowMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;
            if (desktop.MainWindow is not Views.MainWindow mw)
                return;
            if (!mw.IsVisible)
                mw.Show();
            if (mw.WindowState == WindowState.Minimized)
                mw.WindowState = WindowState.Normal;
            mw.Activate();
        });
    }

    private void OpenNewFloatingWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = Services.GetRequiredService<FloatingViewModel>();
            var w = new Views.FloatingWindow(vm);
            w.Show();
        });
    }

    public void QuitApplication()
    {
        _isQuitting = true;

        void DoShutdown()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            try
            {
                var icons = TrayIcon.GetIcons(this);
                if (icons != null && _trayIcon != null)
                    icons.Remove(_trayIcon);
            }
            finally
            {
                _trayIcon?.Dispose();
                _trayIcon = null;
            }

            desktop.Shutdown();
        }

        if (Dispatcher.UIThread.CheckAccess())
            DoShutdown();
        else
            Dispatcher.UIThread.Post(DoShutdown);
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
