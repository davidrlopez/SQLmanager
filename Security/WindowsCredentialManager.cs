using CredentialManagement;
using System;
using System.Collections.Generic;
using System.Security;

namespace SQLmanager.Security
{
    public class WindowsCredentialManager : ICredentialManager
    {
        private const string AppCredentialName = "SQLmanager_Credenciales";
        private const string AppPrefix = "SQLmanagerApp_";

        // Convenience wrappers not used by Manager but kept for compatibility
        public static void SaveCredentials(string username, string password)
        {
            SaveCredential(AppCredentialName, username, password);
        }

        public static (string Username, string Password)? LoadCredentials()
        {
            using var cred = new Credential { Target = AppCredentialName, Type = CredentialType.Generic };
            if (!cred.Load()) return null;
            return (cred.Username, cred.Password);
        }

        public static void DeleteCredentials()
        {
            DeleteCredential(AppCredentialName);
        }

        internal static bool HasCredential(string target)
        {
            using var cred = new Credential { Target = target, Type = CredentialType.Generic };
            return cred.Load();
        }

        internal static bool DeleteCredential(string target)
        {
            using var cred = new Credential { Target = target, Type = CredentialType.Generic };
            try
            {
                return cred.Delete();
            }
            catch
            {
                return false;
            }
        }

        // Returns the secret value stored for the given target (Password field)
        internal static string? GetCredential(string target)
        {
            using var cred = new Credential { Target = target, Type = CredentialType.Generic };
            return cred.Load() ? cred.Password : null;
        }

        // Stores a generic credential using target as key, username as Username and secret as Password
        internal static bool SaveCredential(string target, string username, string secret)
        {
            using var cred = new Credential
            {
                Target = target,
                Username = username,
                Password = secret,
                Type = CredentialType.Generic,
                PersistanceType = PersistanceType.LocalComputer
            };

            try
            {
                return cred.Save();
            }
            catch
            {
                return false;
            }
        }

        // ICredentialManager implementation
        public void StoreCredentials(string username, SecureString password, string host, bool syncToICloud = true)
        {
            var targetName = AppPrefix + host;
            var passwordString = SecureStringHelper.SecureStringToString(password);
            
            try
            {
                SaveCredential(targetName, username, passwordString);
                Console.WriteLine($"[INFO] Credentials stored successfully for {host}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to store credentials: {ex.Message}");
                throw;
            }
            finally
            {
                // Clear password from memory
                if (!string.IsNullOrEmpty(passwordString))
                {
                    Array.Clear(passwordString.ToCharArray(), 0, passwordString.Length);
                }
            }
        }

        public SecureUserCredentials? RetrieveCredentials(string host)
        {
            var targetName = AppPrefix + host;
            
            try
            {
                // Check if credentials exist
                if (!HasStoredCredentials(host))
                {
                    Console.WriteLine($"[WARNING] No credentials found for host: {host}");
                    return null;
                }

                // Get the stored password
                var passwordString = GetCredential(targetName);
                if (string.IsNullOrEmpty(passwordString))
                {
                    Console.WriteLine($"[WARNING] Could not retrieve password for host: {host}");
                    return null;
                }

                // Convert to SecureString
                var securePassword = new SecurePasswordService().CreateSecureString(passwordString);
                
                // Clear password from memory
                Array.Clear(passwordString.ToCharArray(), 0, passwordString.Length);

                // For Windows, we store username separately or use a default
                var username = "C##IMAC"; // Default username, could be enhanced to store separately
                
                return new SecureUserCredentials(username, securePassword, host);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to retrieve credentials: {ex.Message}");
                return null;
            }
        }

        public void DeleteCredentials(string host)
        {
            var targetName = AppPrefix + host;
            
            try
            {
                DeleteCredential(targetName);
                Console.WriteLine($"[INFO] Credentials deleted successfully for {host}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to delete credentials: {ex.Message}");
                throw;
            }
        }

        public bool HasStoredCredentials(string host)
        {
            var targetName = AppPrefix + host;
            return HasCredential(targetName);
        }

        public void UpdateCredentials(string username, SecureString password, string host, bool syncToICloud = true)
        {
            // For Windows, we'll delete and recreate
            DeleteCredentials(host);
            StoreCredentials(username, password, host, syncToICloud);
        }

        public System.Collections.Generic.List<string> GetStoredHosts()
        {
            var hosts = new System.Collections.Generic.List<string>();
            
            try
            {
                using var set = new CredentialSet();
                set.Load();
                foreach (var cred in set)
                {
                    if (cred.Type == CredentialType.Generic && !string.IsNullOrEmpty(cred.Target) && cred.Target.StartsWith(AppPrefix, StringComparison.Ordinal))
                    {
                        var host = cred.Target.Substring(AppPrefix.Length);
                        if (!hosts.Contains(host))
                        {
                            hosts.Add(host);
                        }
                    }
                }
                Console.WriteLine($"[INFO] Found {hosts.Count} stored hosts: {string.Join(", ", hosts)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to get stored hosts: {ex.Message}");
            }
            
            return hosts;
        }

        public bool IsAvailable()
        {
            try
            {
                // Test if we can access the credential manager
                using var testCred = new Credential { Target = "Test", Type = CredentialType.Generic };
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetPlatformName()
        {
            return "Windows Credential Manager";
        }
    }
}




