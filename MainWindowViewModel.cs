using Avalonia.Controls;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SQLmanager.Exceptions;
using SQLmanager.Security;
using SQLmanager.Services;


namespace SQLmanager
{
    public class MainWindowViewModel : IMainWindowViewModel, INotifyPropertyChanged, ILoadingState
    {
        private readonly IDialogFactory _dialogFactory;
        private readonly IWindowFactory _windowFactory;
        private readonly IDatabaseService _databaseService;
        private readonly IConnectionManager _connectionManager;
        private ConnectionState _connectionState = ConnectionState.Disconnected;

        public IDialogFactory DialogFactory => _dialogFactory;

        public event PropertyChangedEventHandler? PropertyChanged;
        public bool IsLoading => _connectionState == ConnectionState.Connecting;
        bool ILoadingState.IsLoading => IsLoading;

        public MainWindowViewModel(
            IDatabaseService databaseService,
            IDialogFactory dialogFactory,
            IInputValidator inputValidator,
            IWindowFactory windowFactory,
        IConnectionManager connectionManager)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
            _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));

            // No cargar credenciales aquí - se hará en OnWindowLoaded para evitar múltiples prompts
            Console.WriteLine("[DEBUG] ViewModel inicializado - credenciales se cargarán en OnWindowLoaded");
        }


        public async Task ConnectAsync(ConnectionCredentials credentials, Action onSuccess, Window parent)
        {
            var parameters = new ConnectionParameters(
                credentials.Username,
                credentials.Password,
                credentials.Host);

            await ConnectWithParametersAsync(parameters, onSuccess, parent);
        }

        public async Task ConnectAsync(SecureConnectionCredentials credentials, Action onSuccess, Window parent)
        {
            var password = SecureStringHelper.SecureStringToString(credentials.Password);

            var parameters = new ConnectionParameters(
                credentials.Username,
                password,
                credentials.Host);

            Array.Clear(password.ToCharArray(), 0, password.Length);

            await ConnectWithParametersAsync(parameters, onSuccess, parent);
        }

        public async Task ConnectWithParametersAsync(ConnectionParameters parameters, Action onSuccess, Window parent)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Starting connection process...");
                Console.WriteLine($"[DEBUG] Username: {parameters.Username}");
                Console.WriteLine($"[DEBUG] Host: {parameters.Host}");

                Console.WriteLine("[DEBUG] Trying to acquire lock...");
                bool lockAcquired = false;
                try
                {
                    lockAcquired = await _connectionManager.TryAcquireLock();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Exception in TryAcquireLock: {ex.Message}");
                    throw;
                }

                Console.WriteLine($"[DEBUG] Lock acquired: {lockAcquired}");
                if (!lockAcquired)
                {
                    throw new ConnectionTimeoutException();
                }

                Console.WriteLine("[DEBUG] Establishing connection...");
                var connectionString = await _connectionManager.EstablishConnection(_databaseService, parameters);

#if WINDOWS
                try
                {
                    // Persist last-used credentials for PIN-gated auto-fill on next startup
                    Manager.SaveCredentials(parameters.Username, parameters.Password);
                    Manager.SaveLastHost(parameters.Host);
                    Console.WriteLine("[INFO] ✅ Last-used credentials and host saved via Manager after successful connection establishment");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Could not save last-used credentials/host: {ex.Message}");
                }
#elif MACOS
                try
                {
                    // Persist last-used credentials for Keychain auto-fill on next startup (macOS)
                    Manager.SaveCredentials(parameters.Username, parameters.Password);
                    Manager.SaveLastHost(parameters.Host);
                    Console.WriteLine("[INFO] ✅ Last-used credentials and host saved via Manager (macOS)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Could not save last-used credentials/host on macOS: {ex.Message}");
                }
#endif

                // Asegúrate aquí que el estado se actualiza correctamente
                // Ejemplo (depende de tu lógica): parameters.State = ConnectionState.Connected;

                Console.WriteLine("[DEBUG] Checking connection state...");
                if (parameters.State == ConnectionState.Connected)
                {
                    Console.WriteLine("[DEBUG] Creating query window...");
                    var queryWindow = _windowFactory.CreateQueryWindow(connectionString);
                    if (queryWindow is IQueryWindow window)
                    {
                        window.Show();
                        Console.WriteLine("[DEBUG] Query window shown.");

#if WINDOWS
                        try
                        {
                            Manager.SaveCredentials(parameters.Username, parameters.Password);
                            Manager.SaveLastHost(parameters.Host);
                            Console.WriteLine("[INFO] ✅ Credentials and host saved successfully using Manager");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARNING] Could not save credentials/host: {ex.Message}");
                        }
#elif MACOS
                        try
                        {
                            // Also save after UI shows, to be consistent (macOS)
                            Manager.SaveCredentials(parameters.Username, parameters.Password);
                            Manager.SaveLastHost(parameters.Host);
                            Console.WriteLine("[INFO] ✅ Credentials and host saved successfully using Manager (macOS)");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARNING] Could not save credentials/host on macOS: {ex.Message}");
                        }
#endif
                        onSuccess();
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] queryWindow is not IQueryWindow");
                    }
                }
                else
                {
                    Console.WriteLine("[WARNING] Connection state is not Connected");
                    var dialog = _dialogFactory.CreateMessageDialog("Error de conexión", "No se pudo establecer la conexión.");
                    await dialog.ShowDialog(parent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Connection failed: {ex.Message}");

                var dialog = _dialogFactory.CreateMessageDialog("Error de conexión", ex.Message);
                await dialog.ShowDialog(parent);

                // No relances la excepción para evitar bloqueos silenciosos
            }
        }


        private async Task<bool> RequestSystemAuthenticationAsync()
        {
            // Ya no necesitamos pedir autenticación aquí porque se hace en Manager.GetCredentials()
            await Task.Delay(1);
            return true;
        }


        private async Task EstablishConnectionAndOpenWindow(
            ConnectionParameters parameters,
            Action onConnectionComplete,
            CancellationToken cancellationToken)
        {
            await _connectionManager.ValidateConnection(_databaseService, parameters);
            var connectionString = await _connectionManager.EstablishConnection(_databaseService, parameters);
            ShowQueryWindow(connectionString, onConnectionComplete);
        }

        private void ShowQueryWindow(string connectionString, Action onWindowShown)
        {
            var queryWindow = _windowFactory.CreateQueryWindow(connectionString) as ConsultaWindow
                ?? throw new InvalidOperationException("Failed to create query window");

            queryWindow.Show();
            onWindowShown();
        }

        private async Task HandleConnectionError(Exception ex)
        {
            var errorMessage = GetErrorMessage(ex);
            await ShowMessage(errorMessage);
        }

        private string GetErrorMessage(Exception ex) => ex switch
        {
            Oracle.ManagedDataAccess.Client.OracleException _ =>
                string.Format(LocalizationConstants.Dialog.ErrorConnection, ex.Message),
            _ => string.Format(LocalizationConstants.Dialog.ErrorUnexpected, ex.Message)
        };

        private async Task ShowMessage(string message)
        {
            var dialog = _dialogFactory.CreateMessageDialog(LocalizationConstants.Dialog.Title, message);
            await dialog.ShowDialog(null!);
        }

        private void SetConnectionState(ConnectionState state)
        {
            _connectionState = state;
            OnPropertyChanged(nameof(IsLoading));
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        // Al final de tu clase MainWindowViewModel, asegúrate de tener:
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public ICommand? LoginCommand { get; set; }

        private string _host = string.Empty;
        public string Host
        {
            get => _host;
            set
            {
                if (_host != value)
                {
                    _host = value;
                    OnPropertyChanged();
                }
            }
        }

    }

    public record ConnectionCredentials(string Username, string Password, string Host);

}
