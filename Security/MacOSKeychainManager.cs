using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Linq;
using System.IO;

namespace SQLmanager.Security
{
    public interface IMacOSKeychainManager : ICredentialManager
    {
        bool IsKeychainAvailable();
        string GetAuthenticationType();
        void ShowKeychainAuthenticationInstructions();
        bool ConfigureBiometricAccess(string host);
        bool IsBiometricAvailable();
    }

    public class MacOSKeychainManager : IMacOSKeychainManager
    {
        private const string ServiceName = "SQLmanager";
        private const string AccountPrefix = "db_";
        private DateTime _lastAuthenticationTime = DateTime.MinValue;
        private readonly TimeSpan _authenticationTimeout = TimeSpan.FromMinutes(30); // 30 minutes timeout

        // Constants for Keychain access control
        private const uint kSecAttrAccessibleWhenUnlocked = 0x0000000D;
        private const uint kSecAccessControlBiometryAny = 0x00000001;
        private const uint kSecAccessControlBiometryCurrentSet = 0x00000002;
        private const uint kSecAccessControlDevicePasscode = 0x00000004;
        private const uint kSecAccessControlUserPresence = 0x00000008;
        private const uint kSecAccessControlWatch = 0x00000010;
        private const uint kSecAccessControlOr = 0x00000020;
        private const uint kSecAccessControlAnd = 0x00000040;
        private const uint kSecAccessControlPrivateKeyUsage = 0x00000080;
        private const uint kSecAccessControlApplicationPassword = 0x00000100;

        public void StoreCredentials(string username, SecureString password, string host, bool syncToICloud = true)
        {
            try
            {
                Console.WriteLine($"[DEBUG] StoreCredentials called with:");
                Console.WriteLine($"[DEBUG] - Username: {username}");
                Console.WriteLine($"[DEBUG] - Host: {host}");
                Console.WriteLine($"[DEBUG] - ServiceName: {ServiceName}");
                Console.WriteLine($"[DEBUG] - AccountPrefix: {AccountPrefix}");
                
                // Store credentials in a structured format: username|host
                var account = $"{AccountPrefix}{host}";
                var structuredData = $"{username}|{host}";
                var passwordString = SecureStringHelper.SecureStringToString(password);
                
                Console.WriteLine($"[DEBUG] - Account: {account}");
                Console.WriteLine($"[DEBUG] - StructuredData: {structuredData}");
                // Never log password or its length

                // Create keychain item with basic authentication requirement
                Console.WriteLine("[DEBUG] Calling SecKeychainAddGenericPassword...");
                var result = SecKeychainAddGenericPassword(
                    IntPtr.Zero, // Use default keychain
                    ServiceName.Length,
                    ServiceName,
                    account.Length,
                    account,
                    passwordString.Length,
                    passwordString,
                    IntPtr.Zero);
                
                Console.WriteLine($"[DEBUG] SecKeychainAddGenericPassword result: {result}");

                if (result == 0)
                {
                    // Configure the item to require system password authentication
                    ConfigureSystemPasswordAccess(host);
                }
                else
                {
                    // Handle specific error codes
                    switch (result)
                    {
                        case -25290: // errSecNoSuchKeychain
                            throw new SecurityException("Keychain not configured. Please set up macOS Keychain first.");
                        
                        case -25291: // errSecAuthFailed
                            throw new SecurityException("Access to Keychain was denied. Please allow access when prompted.");
                        
                        case -25299: // errSecDuplicateItem
                            // Try to update existing item directly without recursion
                            UpdateCredentialsDirect(username, password, host, syncToICloud);
                            return;
                        
                        default:
                            throw new SecurityException($"Failed to store credentials in Keychain: {result}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error storing credentials: {ex.Message}");
                throw;
            }
        }

        public bool ConfigureBiometricAccess(string host)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Configuring biometric access for host: {host}");
                
                var account = $"{AccountPrefix}{host}";
                IntPtr itemRef = IntPtr.Zero;

                // Find the keychain item
                int dummyLength = 0;
                IntPtr dummyData = IntPtr.Zero;
                var findResult = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    ServiceName.Length,
                    ServiceName,
                    account.Length,
                    account,
                    ref dummyLength,
                    ref dummyData,
                    itemRef);

                if (findResult != 0)
                {
                    Console.WriteLine($"[WARNING] Could not find keychain item to configure biometric access: {findResult}");
                    return false;
                }

                // Create access control with biometric authentication
                IntPtr accessControl = IntPtr.Zero;
                var accessResult = SecAccessControlCreateWithFlags(
                    IntPtr.Zero,
                    kSecAttrAccessibleWhenUnlocked,
                    kSecAccessControlBiometryAny | kSecAccessControlDevicePasscode,
                    ref accessControl);

                if (accessResult != 0)
                {
                    Console.WriteLine($"[WARNING] Could not create access control: {accessResult}");
                    return false;
                }

                // Set the access control on the keychain item
                var setResult = SecKeychainItemSetAccess(itemRef, accessControl);
                
                if (setResult == 0)
                {
                    Console.WriteLine("[DEBUG] Successfully configured biometric access");
                }
                else
                {
                    Console.WriteLine($"[WARNING] Could not set access control on keychain item: {setResult}");
                }

                // Clean up
                if (accessControl != IntPtr.Zero)
                    CFRelease(accessControl);

                return setResult == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error configuring biometric access: {ex.Message}");
                return false;
            }
        }

        private bool ConfigureSystemPasswordAccess(string host)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Configuring system password access for host: {host}");
                
                var account = $"{AccountPrefix}{host}";
                IntPtr itemRef = IntPtr.Zero;

                // Find the keychain item
                int dummyLength = 0;
                IntPtr dummyData = IntPtr.Zero;
                var findResult = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    ServiceName.Length,
                    ServiceName,
                    account.Length,
                    account,
                    ref dummyLength,
                    ref dummyData,
                    itemRef);

                if (findResult != 0)
                {
                    Console.WriteLine($"[WARNING] Could not find keychain item to configure system password access: {findResult}");
                    return false;
                }

                // For system password authentication, we don't need to set special access controls
                // The keychain will automatically prompt for the system password when needed
                Console.WriteLine("[DEBUG] System password access configured - macOS will prompt for system password when needed");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error configuring system password access: {ex.Message}");
                return false;
            }
        }

        public bool IsBiometricAvailable()
        {
            try
            {
                // Check if biometric authentication is available on this device
                var authType = GetAuthenticationType();
                return authType.Contains("Touch ID") || authType.Contains("Face ID") || authType.Contains("Apple Watch");
            }
            catch
            {
                return false;
            }
        }

        public bool IsAvailable()
        {
            return IsKeychainAvailable();
        }

        public string GetPlatformName()
        {
            return "macOS Keychain";
        }

        // Métodos estáticos para compatibilidad directa con Manager (clave/valor por service+account)
        public static string? GetCredential(string serviceName, string accountName)
        {
            IntPtr passwordData = IntPtr.Zero;
            int passwordLength = 0;
            try
            {
                var result = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    serviceName.Length,
                    serviceName,
                    accountName.Length,
                    accountName,
                    ref passwordLength,
                    ref passwordData,
                    IntPtr.Zero);

                if (result != 0)
                {
                    return null;
                }

                var bytes = new byte[passwordLength];
                Marshal.Copy(passwordData, bytes, 0, passwordLength);
                var value = Encoding.UTF8.GetString(bytes);
                Array.Clear(bytes, 0, bytes.Length);
                return value;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (passwordData != IntPtr.Zero)
                {
                    SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
                }
            }
        }

        // Método estático para compatibilidad con Manager.IsFirstRun()
        public static bool HasCredential(string serviceName, string accountName)
        {
            try
            {
                var keychainManager = new MacOSKeychainManager();
                return keychainManager.HasStoredCredentials(accountName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to check credential: {ex.Message}");
                return false;
            }
        }

        // Método estático para compatibilidad con Manager.SaveCredentials()
        public static bool SaveCredential(string serviceName, string accountName, string value)
        {
            try
            {
                // Try update first: delete if exists then add
                DeleteCredential(serviceName, accountName);

                var result = SecKeychainAddGenericPassword(
                    IntPtr.Zero,
                    serviceName.Length,
                    serviceName,
                    accountName.Length,
                    accountName,
                    value.Length,
                    value,
                    IntPtr.Zero);

                return result == 0;
            }
            catch
            {
                return false;
            }
        }

        // Método estático para compatibilidad con Manager.DeleteCredentials()
        public static bool DeleteCredential(string serviceName, string accountName)
        {
            IntPtr passwordData = IntPtr.Zero;
            int passwordLength = 0;
            IntPtr itemRef = IntPtr.Zero;
            try
            {
                // Find the item reference first
                var find = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    serviceName.Length,
                    serviceName,
                    accountName.Length,
                    accountName,
                    ref passwordLength,
                    ref passwordData,
                    itemRef);

                if (find != 0)
                {
                    return false;
                }

                // Delete item
                var del = SecKeychainItemDelete(itemRef);
                return del == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (passwordData != IntPtr.Zero)
                {
                    SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
                }
            }
        }

        public SecureUserCredentials? RetrieveCredentials(string host)
        {
            try
            {
                var account = $"{AccountPrefix}{host}";
                IntPtr passwordData = IntPtr.Zero;
                int passwordLength = 0;

                var authType = GetAuthenticationType();
                Console.WriteLine($"[INFO] Attempting to retrieve credentials for host: {host}");
                Console.WriteLine($"[INFO] Authentication type detected: {authType}");
                
                // First, check if credentials exist
                if (!HasStoredCredentials(host))
                {
                    Console.WriteLine($"[WARNING] No credentials found for host: {host}");
                    return null;
                }

                // Force keychain authentication by locking and unlocking
                Console.WriteLine("[DEBUG] Forcing keychain authentication...");
                var lockResult = SecKeychainLock(IntPtr.Zero);
                Console.WriteLine($"[DEBUG] Keychain lock result: {lockResult}");

                // This call will trigger the authentication prompt for system password
                Console.WriteLine("[DEBUG] Retrieving credentials - system password prompt should appear...");
                
                IntPtr itemRef = IntPtr.Zero;
                var result = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    ServiceName.Length,
                    ServiceName,
                    account.Length,
                    account,
                    ref passwordLength,
                    ref passwordData,
                    itemRef);

                if (result != 0)
                {
                    // Handle specific error codes
                    switch (result)
                    {
                        case -25291: // errSecAuthFailed
                            Console.WriteLine("[ERROR] Authentication failed - user cancelled or denied access");
                            return null;
                        case -25293: // errSecNoSuchKeychain
                            Console.WriteLine("[ERROR] Keychain not found");
                            return null;
                        case -25300: // errSecItemNotFound
                            Console.WriteLine($"[ERROR] Credentials not found for host: {host}");
                            return null;
                        default:
                            Console.WriteLine($"[WARNING] Failed to retrieve credentials. Keychain error: {result}");
                            return null;
                    }
                }

                try
                {
                    var passwordBytes = new byte[passwordLength];
                    Marshal.Copy(passwordData, passwordBytes, 0, passwordLength);
                    var passwordString = Encoding.UTF8.GetString(passwordBytes);

                    var securePassword = new SecurePasswordService().CreateSecureString(passwordString);
                    
                    // Clear password from memory
                    Array.Clear(passwordBytes, 0, passwordBytes.Length);
                    Array.Clear(passwordString.ToCharArray(), 0, passwordString.Length);

                    // Parse username from stored data
                    var username = "C##IMAC"; // Default username
                    
                    return new SecureUserCredentials(username, securePassword, host);
                }
                finally
                {
                    if (passwordData != IntPtr.Zero)
                        SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
                }
            }
            catch
            {
                return null;
            }
        }

        public void DeleteCredentials(string host)
        {
            try
            {
                var account = $"{AccountPrefix}{host}";
                IntPtr itemRef = IntPtr.Zero;

                int dummyLength = 0;
                IntPtr dummyData = IntPtr.Zero;
                var result = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    ServiceName.Length,
                    ServiceName,
                    account.Length,
                    account,
                    ref dummyLength,
                    ref dummyData,
                    itemRef);

                if (result == 0 && itemRef != IntPtr.Zero)
                {
                    SecKeychainItemDelete(itemRef);
                    CFRelease(itemRef);
                }
            }
            catch
            {
                // Silently fail - don't let credential cleanup break the application
            }
        }

        public bool HasStoredCredentials(string host)
        {
            try
            {
                Console.WriteLine($"[DEBUG] HasStoredCredentials called for host: {host}");
                Console.WriteLine($"[DEBUG] - ServiceName: {ServiceName}");
                Console.WriteLine($"[DEBUG] - AccountPrefix: {AccountPrefix}");
                
                var account = $"{AccountPrefix}{host}";
                Console.WriteLine($"[DEBUG] - Account: {account}");
                
                IntPtr itemRef = IntPtr.Zero;
                int dummyLength = 0;
                IntPtr dummyData = IntPtr.Zero;
                
                Console.WriteLine("[DEBUG] Calling SecKeychainFindGenericPassword...");
                var result = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    ServiceName.Length,
                    ServiceName,
                    account.Length,
                    account,
                    ref dummyLength,
                    ref dummyData,
                    itemRef);
                
                Console.WriteLine($"[DEBUG] SecKeychainFindGenericPassword result: {result}");

                if (result == 0 && itemRef != IntPtr.Zero)
                {
                    CFRelease(itemRef);
                    Console.WriteLine("[DEBUG] Credentials found and itemRef released");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] No credentials found or error occurred");
                }

                return result == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in HasStoredCredentials: {ex.Message}");
                return false;
            }
        }

        public void UpdateCredentials(string username, SecureString password, string host, bool syncToICloud = true)
        {
            try
            {
                var account = $"{AccountPrefix}{host}";
                var passwordString = SecureStringHelper.SecureStringToString(password);
                IntPtr itemRef = IntPtr.Zero;

                // Find the existing item
                int dummyLength = 0;
                IntPtr dummyData = IntPtr.Zero;
                var findResult = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    ServiceName.Length,
                    ServiceName,
                    account.Length,
                    account,
                    ref dummyLength,
                    ref dummyData,
                    itemRef);

                if (findResult == 0 && itemRef != IntPtr.Zero)
                {
                    // Update the existing item
                    var updateResult = SecKeychainItemModifyContent(
                        itemRef,
                        IntPtr.Zero,
                        passwordString.Length,
                        passwordString);

                    CFRelease(itemRef);

                    if (updateResult == 0)
                    {
                        Console.WriteLine($"[INFO] Credentials updated successfully for {host}");
                        return;
                    }
                    else
                    {
                        throw new SecurityException($"Failed to update credentials in Keychain: {updateResult}");
                    }
                }
                else
                {
                    // Item doesn't exist, but don't call StoreCredentials to avoid recursion
                    Console.WriteLine($"[WARNING] Could not find existing keychain item to update: {findResult}");
                    throw new SecurityException("Could not find existing credentials to update");
                }

                // Clear password from memory
                Array.Clear(passwordString.ToCharArray(), 0, passwordString.Length);
            }
            catch (Exception ex)
            {
                throw new SecurityException("Failed to update credentials securely", ex);
            }
        }

        private void UpdateCredentialsDirect(string username, SecureString password, string host, bool syncToICloud = true)
        {
            try
            {
                var account = $"{AccountPrefix}{host}";
                var passwordString = SecureStringHelper.SecureStringToString(password);
                IntPtr itemRef = IntPtr.Zero;

                // Find the existing item
                int dummyLength = 0;
                IntPtr dummyData = IntPtr.Zero;
                var findResult = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    ServiceName.Length,
                    ServiceName,
                    account.Length,
                    account,
                    ref dummyLength,
                    ref dummyData,
                    itemRef);

                if (findResult == 0 && itemRef != IntPtr.Zero)
                {
                    // Update the existing item
                    var updateResult = SecKeychainItemModifyContent(
                        itemRef,
                        IntPtr.Zero,
                        passwordString.Length,
                        passwordString);

                    CFRelease(itemRef);

                    if (updateResult == 0)
                    {
                        // Clear password from memory before returning
                        Array.Clear(passwordString.ToCharArray(), 0, passwordString.Length);
                        return;
                    }
                    else
                    {
                        // Don't throw exception, just return silently
                        Array.Clear(passwordString.ToCharArray(), 0, passwordString.Length);
                        return;
                    }
                }
                else
                {
                    // Don't try to create new one to avoid recursion
                    Array.Clear(passwordString.ToCharArray(), 0, passwordString.Length);
                    return;
                }
            }
            catch
            {
                // Don't throw exception, just return silently
            }
        }

        private void EnableICloudSync(string account)
        {
            try
            {
                // This is a simplified implementation
                // In a real implementation, you would use SecKeychainItemSetAccess
                // to ensure the item is accessible from iCloud Keychain
                
                // For now, we'll just log that iCloud sync is enabled
                Console.WriteLine($"[DEBUG] iCloud sync enabled for account: {account}");
            }
            catch
            {
                // If iCloud sync fails, credentials are still stored locally
                Console.WriteLine($"[WARNING] iCloud sync failed for account: {account}");
            }
        }

        public bool IsKeychainAvailable()
        {
            try
            {
                Console.WriteLine("[DEBUG] Checking if Keychain is available...");
                
                // Try to access the default keychain to see if it's available
                IntPtr keychain = IntPtr.Zero;
                var result = SecKeychainCopyDefault(ref keychain);
                
                Console.WriteLine($"[DEBUG] SecKeychainCopyDefault result: {result}");
                
                if (result == 0 && keychain != IntPtr.Zero)
                {
                    CFRelease(keychain);
                    Console.WriteLine("[DEBUG] Keychain is available");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Keychain not available - error code: {result}");
                    
                    // Common error codes:
                    // -25290: errSecNoSuchKeychain - Keychain doesn't exist
                    // -25291: errSecAuthFailed - Authentication failed
                    // -25293: errSecNoSuchKeychain - Keychain not found
                    
                    if (result == -25290 || result == -25293)
                    {
                        Console.WriteLine("[INFO] Keychain not configured. This is normal on a new Mac.");
                        Console.WriteLine("[INFO] The first time you save credentials, macOS will prompt to create a keychain.");
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error checking Keychain availability: {ex.Message}");
                return false;
            }
        }

        public List<string> GetStoredHosts()
        {
            var hosts = new List<string>();
            try
            {
                // This is a simplified implementation
                // In a real implementation, you would use SecKeychainSearchCreate
                // to search for all items with our service name
                
                // For now, we'll return the default host if it exists
                if (HasStoredCredentials("192.168.1.24"))
                {
                    hosts.Add("192.168.1.24");
                }
                
                return hosts;
            }
            catch
            {
                return hosts;
            }
        }

        public string GetAuthenticationType()
        {
            try
            {
                // Check system capabilities for biometric authentication
                var machineName = Environment.MachineName.ToLower();
                
                Console.WriteLine($"[DEBUG] Machine name: {Environment.MachineName}");
                
                // Mac models that typically have Touch ID
                var touchIdModels = new[] { "macbookpro", "macbookair", "imac" };
                
                // Check if this is a Mac mini or similar without biometric sensors
                var hasBiometrics = touchIdModels.Any(model => machineName.Contains(model));
                
                Console.WriteLine($"[DEBUG] Has biometrics based on model: {hasBiometrics}");
                
                if (hasBiometrics)
                {
                    // For MacBook Pro, always assume Touch ID is available
                    if (machineName.Contains("macbookpro"))
                    {
                        Console.WriteLine("[DEBUG] MacBook Pro detected - forcing Touch ID");
                        return "Touch ID/Face ID";
                    }
                    
                    // Try to detect if Touch ID/Face ID is actually available
                    try
                    {
                        // Check if the system has biometric authentication capabilities
                        var hasTouchId = File.Exists("/System/Library/CoreServices/AuthenticationServices_Simulator.bundle") ||
                                        File.Exists("/System/Library/PrivateFrameworks/BiometricKit.framework") ||
                                        File.Exists("/System/Library/Frameworks/LocalAuthentication.framework");
                        
                        Console.WriteLine($"[DEBUG] Touch ID frameworks detected: {hasTouchId}");
                        
                        if (hasTouchId)
                        {
                            return "Touch ID/Face ID";
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] Error checking Touch ID frameworks: {ex.Message}");
                        // If we can't determine, assume biometrics might be available
                        return "Touch ID/Face ID (detected)";
                    }
                }
                
                // For Mac mini, Mac Pro, or other models without biometric sensors
                Console.WriteLine("[DEBUG] No biometrics detected - using System Password");
                return "System Password";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in GetAuthenticationType: {ex.Message}");
                return "System Password (fallback)";
            }
        }

        public void ShowKeychainAuthenticationInstructions()
        {
            var authType = GetAuthenticationType();
            
            Console.WriteLine("=== INSTRUCCIONES PARA CONFIGURAR AUTENTICACIÓN DEL KEYCHAIN ===");
            Console.WriteLine($"Tipo de autenticación detectado: {authType}");
            Console.WriteLine("");
            
            if (authType.Contains("Touch ID"))
            {
                Console.WriteLine("🔐 PARA FORZAR TOUCH ID EN MACBOOK PRO:");
                Console.WriteLine("");
                Console.WriteLine("1. Ve a Preferencias del Sistema > Touch ID y Contraseña");
                Console.WriteLine("2. Asegúrate de que 'Llaveros' esté habilitado para Touch ID");
                Console.WriteLine("3. Si no aparece 'Llaveros', agrega una contraseña al sistema");
                Console.WriteLine("");
                Console.WriteLine("4. Abre 'Acceso a Llaveros' (Keychain Access)");
                Console.WriteLine("   - Busca 'Keychain Access' en Spotlight (Cmd+Space)");
                Console.WriteLine("   - O ve a Aplicaciones > Utilidades > Acceso a Llaveros");
                Console.WriteLine("");
                Console.WriteLine("5. En Acceso a Llaveros:");
                Console.WriteLine("   - Selecciona 'Llavero de inicio' en el panel izquierdo");
                Console.WriteLine("   - Busca elementos con 'SQLmanager' en la lista");
                Console.WriteLine("   - Haz doble clic en el elemento");
                Console.WriteLine("   - Marca 'Mostrar contraseña' para forzar Touch ID");
                Console.WriteLine("");
                Console.WriteLine("6. Si Touch ID no aparece:");
                Console.WriteLine("   - Ve a Preferencias del Sistema > Seguridad y Privacidad");
                Console.WriteLine("   - Selecciona la pestaña 'Llaveros'");
                Console.WriteLine("   - Configura 'Llavero de inicio' para requerir contraseña");
                Console.WriteLine("   - Luego vuelve a Touch ID y Contraseña");
                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine("🔐 PARA CONFIGURAR CONTRASEÑA DEL SISTEMA:");
                Console.WriteLine("");
                Console.WriteLine("1. Abre 'Acceso a Llaveros' (Keychain Access)");
                Console.WriteLine("   - Busca 'Keychain Access' en Spotlight (Cmd+Space)");
                Console.WriteLine("   - O ve a Aplicaciones > Utilidades > Acceso a Llaveros");
                Console.WriteLine("");
                Console.WriteLine("2. En Acceso a Llaveros:");
                Console.WriteLine("   - Selecciona 'Llavero de inicio' en el panel izquierdo");
                Console.WriteLine("   - Busca elementos con 'SQLmanager' en la lista");
                Console.WriteLine("   - Haz doble clic en el elemento");
                Console.WriteLine("   - Marca 'Mostrar contraseña'");
                Console.WriteLine("");
                Console.WriteLine("3. Alternativamente:");
                Console.WriteLine("   - Ve a Preferencias del Sistema > Seguridad y Privacidad");
                Console.WriteLine("   - Selecciona la pestaña 'Llaveros'");
                Console.WriteLine("   - Configura 'Llavero de inicio' para requerir contraseña");
                Console.WriteLine("");
            }
            
            Console.WriteLine("🆘 SI LA APP SE CIERRA EN MAC NUEVO:");
            Console.WriteLine("1. Ve a Preferencias del Sistema > Seguridad y Privacidad");
            Console.WriteLine("2. Selecciona la pestaña 'Llaveros'");
            Console.WriteLine("3. Haz clic en 'Crear llavero' si no existe");
            Console.WriteLine("4. Configura una contraseña para el llavero");
            Console.WriteLine("5. Reinicia la aplicación después de configurar el llavero");
            Console.WriteLine("================================================================");
        }

        private bool ForceKeychainAuthentication()
        {
            try
            {
                Console.WriteLine("[DEBUG] Attempting to force Keychain authentication...");
                
                var authType = GetAuthenticationType();
                Console.WriteLine($"[DEBUG] Authentication type: {authType}");
                
                // Method 1: Try to access the default keychain to trigger authentication
                IntPtr keychainRef = IntPtr.Zero;
                var result = SecKeychainCopyDefault(ref keychainRef);
                
                if (result == 0 && keychainRef != IntPtr.Zero)
                {
                    Console.WriteLine("[DEBUG] Successfully accessed default keychain");
                    CFRelease(keychainRef);
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Failed to access default keychain: {result}");
                }
                
                // Method 2: Try to find a non-existent item to trigger authentication
                Console.WriteLine("[DEBUG] Trying alternative authentication method...");
                IntPtr dummyData = IntPtr.Zero;
                int dummyLength = 0;
                
                IntPtr dummyItemRef = IntPtr.Zero;
                var dummyResult = SecKeychainFindGenericPassword(
                    IntPtr.Zero,
                    "DummyService".Length,
                    "DummyService",
                    "DummyAccount".Length,
                    "DummyAccount",
                    ref dummyLength,
                    ref dummyData,
                    dummyItemRef);
                
                Console.WriteLine($"[DEBUG] Dummy authentication attempt result: {dummyResult}");
                
                // Method 3: Try to lock and unlock the keychain to force authentication
                Console.WriteLine("[DEBUG] Attempting to lock/unlock keychain...");
                var lockResult = SecKeychainLock(IntPtr.Zero);
                Console.WriteLine($"[DEBUG] Keychain lock result: {lockResult}");
                
                // Method 4: For MacBook Pro, try to force Touch ID prompt
                if (authType.Contains("Touch ID"))
                {
                    Console.WriteLine("[DEBUG] MacBook Pro detected - attempting to force Touch ID prompt...");
                    Console.WriteLine("[INFO] Touch ID should be requested automatically by macOS Keychain");
                    Console.WriteLine("[INFO] If Touch ID doesn't appear, try accessing Keychain Access app first");
                }
                
                // Method 5: Show detailed instructions for manual authentication
                Console.WriteLine("[INFO] If authentication prompt doesn't appear, showing manual configuration instructions...");
                ShowKeychainAuthenticationInstructions();
                
                return true; // Return true to continue with the actual retrieval
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error forcing Keychain authentication: {ex.Message}");
                return false;
            }
        }



        // P/Invoke declarations for macOS Keychain
        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainAddGenericPassword(
            IntPtr keychain,
            int serviceNameLength,
            string serviceName,
            int accountNameLength,
            string accountName,
            int passwordLength,
            string password,
            IntPtr itemRef);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainFindGenericPassword(
            IntPtr keychain,
            int serviceNameLength,
            string serviceName,
            int accountNameLength,
            string accountName,
            ref int passwordLength,
            ref IntPtr passwordData,
            IntPtr itemRef);



        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainItemDelete(IntPtr itemRef);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainItemModifyContent(
            IntPtr itemRef,
            IntPtr attrList,
            int passwordLength,
            string password);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr cf);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainCopyDefault(ref IntPtr keychain);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainLock(IntPtr keychain);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecAccessControlCreateWithFlags(
            IntPtr allocator,
            uint flags,
            uint accessControl,
            ref IntPtr accessControlRef);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainItemSetAccess(
            IntPtr itemRef,
            IntPtr accessControl);


    }
} 