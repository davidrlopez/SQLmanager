using System;
using System.Threading.Tasks;
using SQLmanager.Security;
using SQLmanager.Services;

namespace SQLmanager.Security
{
    public static class Manager
    {
        private const string APP_IDENTIFIER = "SQLmanager";
        private const string USERNAME_KEY = "SQLmanager_Username";
        private const string PASSWORD_KEY = "SQLmanager_Password";
        private const string HOST_KEY = "SQLmanager_Host";

        public static bool IsFirstRun()
        {
#if WINDOWS
            return !WindowsCredentialManager.HasCredential(USERNAME_KEY);
#elif MACOS
            return !MacOSKeychainManager.HasCredential(APP_IDENTIFIER, USERNAME_KEY);
#else
            throw new PlatformNotSupportedException("Plataforma no soportada");
#endif
        }

        public static bool SaveCredentials(string username, string password)
        {
            try
            {
#if WINDOWS
                bool usernameResult = WindowsCredentialManager.SaveCredential(USERNAME_KEY, username, username);
                bool passwordResult = WindowsCredentialManager.SaveCredential(PASSWORD_KEY, username, password);
                return usernameResult && passwordResult;
#elif MACOS
                bool usernameResult = MacOSKeychainManager.SaveCredential(APP_IDENTIFIER, USERNAME_KEY, username);
                bool passwordResult = MacOSKeychainManager.SaveCredential(APP_IDENTIFIER, PASSWORD_KEY, password);
                return usernameResult && passwordResult;
#else
                return false;
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving credentials: {ex.Message}");
                return false;
            }
        }

        public static bool SaveLastHost(string host)
        {
            try
            {
#if WINDOWS
                return WindowsCredentialManager.SaveCredential(HOST_KEY, host, host);
#elif MACOS
                return MacOSKeychainManager.SaveCredential(APP_IDENTIFIER, HOST_KEY, host);
#else
                return false;
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving last host: {ex.Message}");
                return false;
            }
        }

        public static (string username, string password) GetCredentials()
        {
            try
            {
#if WINDOWS
                // Solo pedir autenticación una vez cuando realmente accedemos a las credenciales
                bool authOk = WindowsAuthenticationHelper.PromptForWindowsCredentials(out _, out _);
                if (!authOk)
                {
                    Console.WriteLine("[INFO] System authentication cancelled/failed. Not loading credentials.");
                    return (null, null);
                }
                
                string username = WindowsCredentialManager.GetCredential(USERNAME_KEY);
                string password = WindowsCredentialManager.GetCredential(PASSWORD_KEY);
                return (username, password);
#elif MACOS
                string username = MacOSKeychainManager.GetCredential(APP_IDENTIFIER, USERNAME_KEY);
                string password = MacOSKeychainManager.GetCredential(APP_IDENTIFIER, PASSWORD_KEY);
                return (username, password);
#else
                return (null, null);
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting credentials: {ex.Message}");
                return (null, null);
            }
        }

        public static string? GetLastHost()
        {
            try
            {
#if WINDOWS
                // No pedimos PIN otra vez; reutilizamos el prompt de GetCredentials en el arranque
                return WindowsCredentialManager.GetCredential(HOST_KEY);
#elif MACOS
                return MacOSKeychainManager.GetCredential(APP_IDENTIFIER, HOST_KEY);
#else
                return null;
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting last host: {ex.Message}");
                return null;
            }
        }

        public static bool DeleteCredentials()
        {
            try
            {
#if WINDOWS
                bool usernameResult = WindowsCredentialManager.DeleteCredential(USERNAME_KEY);
                bool passwordResult = WindowsCredentialManager.DeleteCredential(PASSWORD_KEY);
                bool hostResult = WindowsCredentialManager.DeleteCredential(HOST_KEY);
                return usernameResult && passwordResult && hostResult;
#elif MACOS
                bool usernameResult = MacOSKeychainManager.DeleteCredential(APP_IDENTIFIER, USERNAME_KEY);
                bool passwordResult = MacOSKeychainManager.DeleteCredential(APP_IDENTIFIER, PASSWORD_KEY);
                bool hostResult = MacOSKeychainManager.DeleteCredential(APP_IDENTIFIER, HOST_KEY);
                return usernameResult && passwordResult && hostResult;
#else
                return false;
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting credentials: {ex.Message}");
                return false;
            }
        }
    }
}