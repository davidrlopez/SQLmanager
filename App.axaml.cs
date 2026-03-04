using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SQLmanager.Security;
using SQLmanager.Services;

namespace SQLmanager;

public partial class App : Application, IDisposable
{
    private IServiceProvider? _serviceProvider;
    private bool _disposed;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            _serviceProvider = Program.ServiceProvider ?? ConfigureServices();

            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    desktop.MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    break;
                case ISingleViewApplicationLifetime singleView:
                    break;
                default:
                    throw new NotSupportedException($"Application lifetime type '{ApplicationLifetime?.GetType().Name}' is not supported");
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {

            LogError(ex);
            throw new ApplicationException("Failed to initialize application. Please contact support.", ex);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (disposing)
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        _disposed = true;
    }

    private static void LogError(Exception ex)
    {
        try
        {
            var logger = new FileAuditLogger();
            logger.LogError($"Application initialization failed: {ex.Message}");
        }
        catch
        {
            // Silently fail - don't let logging errors break the application
        }
    }
    
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        
        // Register core services
        services.RegisterCoreServices();
        
        // Register windows
        services.AddTransient<MainWindow>();
        
        return services.BuildServiceProvider();
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterCoreServices(this IServiceCollection services)
    {
        // Register audit logger
        services.AddSingleton<IAuditLogger, FileAuditLogger>();
        
        // Register core services with audit logging
        services.AddSingleton<IDatabaseService>(provider => 
            new DatabaseService(provider.GetRequiredService<IAuditLogger>()));
        services.AddSingleton<IDialogFactory, DialogFactory>();
        services.AddSingleton<IInputValidator, InputValidator>();
        services.AddSingleton<IWindowFactory, WindowFactory>();
        services.AddSingleton<ISecurePasswordService, SecurePasswordService>();
        services.AddSingleton<ICredentialValidator, CredentialValidator>();
        services.AddSingleton<IConnectionManager>(provider => 
            new ConnectionManager(provider.GetRequiredService<IAuditLogger>()));
        
        // Register macOS Keychain Manager
        services.AddSingleton<IMacOSKeychainManager, MacOSKeychainManager>();
        services.AddSingleton<CredentialManagerUI>();
        services.AddTransient<IConnectionFormFactory, ConnectionFormFactory>();

        services.AddSingleton<IConnectionFormProvider>(provider =>
        {
            var username = Environment.GetEnvironmentVariable("SQLMANAGER_DB_USER") ?? string.Empty;
            var host = Environment.GetEnvironmentVariable("SQLMANAGER_DB_HOST") ?? string.Empty;
            var password = Environment.GetEnvironmentVariable("SQLMANAGER_DB_PASSWORD") ?? "";
            
            var logger = provider.GetRequiredService<IAuditLogger>();
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(host))
            {
                logger.LogConnectionAttempt(username, host, false);
            }
            
            return new ConnectionForm(username, host, password, host);
        });

        services.AddTransient<MainWindowViewModel>(provider => 
        {
            var databaseService = provider.GetRequiredService<IDatabaseService>();
            var dialogFactory = provider.GetRequiredService<IDialogFactory>();
            var inputValidator = provider.GetRequiredService<IInputValidator>();
            var windowFactory = provider.GetRequiredService<IWindowFactory>();
            var connectionManager = provider.GetRequiredService<IConnectionManager>();
            return new MainWindowViewModel(databaseService, dialogFactory, inputValidator, windowFactory, connectionManager);
        });

        services.AddTransient<MainWindow>(provider =>
        {
            var deps = provider.GetRequiredService<WindowDependencies>();
            return new MainWindow(deps)
            {
                ViewModel = provider.GetRequiredService<MainWindowViewModel>(),
                FormProvider = provider.GetRequiredService<IConnectionFormProvider>()
            };
        });

        services.AddTransient<ConnectionForm>(provider => 
        {
            var username = Environment.GetEnvironmentVariable("SQLMANAGER_DB_USER") ?? string.Empty;
            var host = Environment.GetEnvironmentVariable("SQLMANAGER_DB_HOST") ?? string.Empty;
            var password = Environment.GetEnvironmentVariable("SQLMANAGER_DB_PASSWORD") ?? "";
            
            return new ConnectionForm(username, host, password, host);
        });

        services.AddTransient<WindowDependencies>(provider => new WindowDependencies
        {
            DatabaseService = provider.GetRequiredService<IDatabaseService>(),
            DialogFactory = provider.GetRequiredService<IDialogFactory>(),
            InputValidator = provider.GetRequiredService<IInputValidator>(),
            WindowFactory = provider.GetRequiredService<IWindowFactory>(),
            FormProvider = provider.GetRequiredService<IConnectionFormProvider>(),
            CredentialValidator = provider.GetRequiredService<ICredentialValidator>(),
            PasswordService = provider.GetRequiredService<ISecurePasswordService>(),
            ConnectionManager = provider.GetRequiredService<IConnectionManager>(),
            CredentialManagerUI = provider.GetRequiredService<CredentialManagerUI>()
        });

        return services;
    }
}
