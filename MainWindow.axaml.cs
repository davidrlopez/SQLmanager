using System;
using System.Security;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Threading.Tasks;
using SQLmanager.Security;
using SQLmanager.Services;


namespace SQLmanager
{
    public static class DialogMessages
    {
        public const string InvalidCredentialsMessage = "Please provide valid username, password and host";
        public const string ValidationErrorTitle = "Validation Error";
    }

    public class WindowDependencies
    {
        public required IDatabaseService DatabaseService { get; init; }
        public required IDialogFactory DialogFactory { get; init; }
        public required IInputValidator InputValidator { get; init; }
        public required IWindowFactory WindowFactory { get; init; }
        public required IConnectionFormProvider FormProvider { get; init; }
        public required ICredentialValidator CredentialValidator { get; init; }
        public required ISecurePasswordService PasswordService { get; init; }
        public required IConnectionManager ConnectionManager { get; init; }
        public required CredentialManagerUI CredentialManagerUI { get; init; }
    }

    public partial class MainWindow : Window
    {
        private readonly ICredentialValidator _credentialValidator;
        private readonly ISecurePasswordService _passwordService;
        private readonly CredentialManagerUI _credentialManagerUI;
        private readonly ICredentialManager _credentialManager;

        public required IMainWindowViewModel ViewModel { get; init; }
        public required IConnectionFormProvider FormProvider { get; init; }

        public MainWindow() : this(CreateDefaultDependencies())
        {
        }

        public MainWindow(WindowDependencies dependencies)
        {
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));

            InitializeComponent();

            FormProvider = dependencies.FormProvider;
            _credentialValidator = dependencies.CredentialValidator;
            _passwordService = dependencies.PasswordService;
            _credentialManagerUI = dependencies.CredentialManagerUI;
            _credentialManager = CredentialManagerFactory.CreateCredentialManager();

            ViewModel = new MainWindowViewModel(
                dependencies.DatabaseService,
                dependencies.DialogFactory,
                dependencies.InputValidator,
                dependencies.WindowFactory,
                dependencies.ConnectionManager);

            DataContext = ViewModel;

            // Load saved credentials when window is loaded
            this.Loaded += OnWindowLoaded;
        }

        private static WindowDependencies CreateDefaultDependencies() => new()
        {
            DatabaseService = new DatabaseService(new FileAuditLogger()),
            DialogFactory = new DialogFactory(),
            InputValidator = new InputValidator(),
            WindowFactory = new WindowFactory(null!),
            FormProvider = new ConnectionForm(string.Empty, string.Empty, string.Empty, string.Empty),
            CredentialValidator = new CredentialValidator(),
            PasswordService = new SecurePasswordService(),
            ConnectionManager = new ConnectionManager(),
            CredentialManagerUI = new CredentialManagerUI()
        };

        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            // Load last-used credentials on startup, gated by system PIN/password
            Task.Run(() =>
            {
                try
                {
                    var lastHost = Manager.GetLastHost();
                    var defaultHost = !string.IsNullOrWhiteSpace(lastHost) ? lastHost : string.Empty;

                    Console.WriteLine("[INFO] Attempting to load last-used credentials (system auth required)...");

                    object? savedCredentials = null;

                    try
                    {
                        // Trigger system authentication once
                        var (legacyUser, legacyPass) = Manager.GetCredentials();

                        // Prefer host-specific credentials for the last host
                        if (!string.IsNullOrWhiteSpace(defaultHost) && _credentialManager.HasStoredCredentials(defaultHost))
                        {
                            var hostCreds = _credentialManager.RetrieveCredentials(defaultHost);
                            if (hostCreds != null)
                            {
                                var pwd = SecureStringHelper.SecureStringToString(hostCreds.Password);
                                // En macOS, usamos el usuario guardado por Manager y la contraseña del llavero (para forzar prompt)
#if MACOS
                                var finalUsername = !string.IsNullOrEmpty(legacyUser) ? legacyUser : hostCreds.Username;
                                savedCredentials = new { Host = defaultHost, Username = finalUsername, Password = pwd };
#else
                                savedCredentials = new { Host = defaultHost, Username = hostCreds.Username, Password = pwd };
#endif
                                Array.Clear(pwd.ToCharArray(), 0, pwd.Length);
                            }
                        }

                        // Fallback to legacy Manager credentials if no host-specific are available
                        if (savedCredentials == null && !string.IsNullOrEmpty(legacyUser) && !string.IsNullOrEmpty(legacyPass))
                        {
                            savedCredentials = new { Host = defaultHost, Username = legacyUser, Password = legacyPass };
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] Error loading credentials: {ex.Message}");
                        savedCredentials = null;
                    }

                    if (savedCredentials != null)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                var hostInput = this.FindControl<TextBox>("HostInput");
                                var usernameInput = this.FindControl<TextBox>("UsernameInput");
                                var passwordInput = this.FindControl<TextBox>("PasswordInput");

                                if (hostInput != null && usernameInput != null && passwordInput != null)
                                {
                                    dynamic creds = savedCredentials;
                                    hostInput.Text = creds.Host;
                                    usernameInput.Text = creds.Username;
                                    passwordInput.Text = creds.Password;

                                    Console.WriteLine($"[INFO] ✅ Loaded last-used credentials for {creds.Host} (after system auth)");
                                }
                            }
                            catch (Exception uiEx)
                            {
                                Console.WriteLine($"[WARNING] Error updating UI with credentials: {uiEx.Message}");
                            }
                        });
                    }
                    else
                    {
                        Console.WriteLine("[INFO] No saved credentials to load on startup or authentication cancelled");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Could not load saved credentials on startup: {ex.Message}");
                }
            });
        }

        public async void OnConnectClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                using var credentials = CreateSecureCredentialsFromForm();

                if (credentials == null || !_credentialValidator.AreCredentialsValid(credentials))
                {
                    await HandleInvalidCredentials();
                    return;
                }

                _ = Task.Run(() =>
                {
                    try
                    {
                        if (!_credentialManager.HasStoredCredentials(credentials.Host))
                        {
                            Console.WriteLine($"[INFO] Saving new credentials to {_credentialManager.GetPlatformName()} for {credentials.Host}");

                            _credentialManager.StoreCredentials(
                                credentials.Username,
                                credentials.Password,
                                credentials.Host,
                                syncToICloud: true);

                            Console.WriteLine($"[INFO] Credentials saved successfully to {_credentialManager.GetPlatformName()} for {credentials.Host}");
                        }
                        else
                        {
                            Console.WriteLine($"[INFO] Credentials already exist for {credentials.Host} - skipping save");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WARNING] Could not save credentials to {_credentialManager.GetPlatformName()}: {ex.Message}");
                        Console.WriteLine("[WARNING] The connection will still work - credentials just won't be saved.");
                    }
                });

                var connectionCredentials = new SecureConnectionCredentials(
                    credentials.Username,
                    credentials.Password,
                    credentials.Host);

                await ViewModel.ConnectAsync(connectionCredentials, Close, this);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception in OnConnectClick: {ex}");
            }
        }

        private SecureUserCredentials? CreateSecureCredentialsFromForm()
        {
            var username = this.FindControl<TextBox>("UsernameInput")?.Text ?? string.Empty;
            var password = this.FindControl<TextBox>("PasswordInput")?.Text ?? string.Empty;
            var host = this.FindControl<TextBox>("HostInput")?.Text ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(host))
            {
                var securePassword = _passwordService.CreateSecureString(password);
                return new SecureUserCredentials(username, securePassword, host);
            }

            return null;
        }

        private async Task HandleInvalidCredentials()
        {
            var dialog = ViewModel.DialogFactory.CreateMessageDialog(
                DialogMessages.ValidationErrorTitle,
                DialogMessages.InvalidCredentialsMessage);
            await dialog.ShowDialog(this);
        }

        public void OnManageCredentialsClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                _credentialManagerUI.ShowCredentialManager(this);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception in OnManageCredentialsClick: {ex}");
            }
        }




    }
}
