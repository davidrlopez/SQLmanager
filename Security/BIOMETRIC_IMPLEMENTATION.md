# 🔐 Implementación de Autenticación Biométrica nativa de macOS

## 📋 Resumen

Esta implementación permite usar **Touch ID**, **Face ID** y **Apple Watch** para autenticar el acceso a credenciales almacenadas en el Keychain de macOS, siguiendo las mejores prácticas de seguridad nativas del sistema operativo.

## 🎯 Características Principales

### ✅ Lo que SÍ permite macOS nativamente:

- **Touch ID**: En MacBook Pro con Touch Bar
- **Apple Watch**: Cuando está desbloqueado y cerca del Mac
- **Face ID**: A través del iPhone cercano (desbloqueado)
- **Contraseña del sistema**: Como fallback cuando no hay biometría disponible

### ❌ Lo que NO puedes forzar:

- Un tipo específico de autenticación (macOS elige automáticamente)
- Touch ID en dispositivos sin Touch Bar
- Face ID directamente en Mac (solo a través del iPhone)

## 🏗️ Arquitectura de la Implementación

### 1. `MacOSBiometricService.cs`
Servicio principal que implementa las APIs nativas del Security framework:

```csharp
public class MacOSBiometricService : IMacOSBiometricService
{
    // Almacena credenciales con protección biométrica
    public bool StoreCredentialsWithBiometric(string username, SecureString password, string host);
    
    // Recupera credenciales (desencadena autenticación biométrica)
    public SecureUserCredentials? RetrieveCredentialsWithBiometric(string host);
    
    // Verifica disponibilidad de biometría
    public bool IsBiometricAvailable();
    
    // Configura ítems existentes del Keychain
    public bool ConfigureKeychainForBiometric(string host);
}
```

### 2. `MacOSKeychainManager.cs` (Mejorado)
Versión mejorada del KeychainManager existente con soporte biométrico:

```csharp
public class MacOSKeychainManager : IMacOSKeychainManager
{
    // Nuevos métodos para configuración biométrica
    public bool ConfigureBiometricAccess(string host);
    public bool IsBiometricAvailable();
}
```

## 🔧 APIs Nativas Utilizadas

### P/Invoke Declarations

```csharp
// Crear control de acceso con biometría
[DllImport("/System/Library/Frameworks/Security.framework/Security")]
private static extern int SecAccessControlCreateWithFlags(
    IntPtr allocator,
    uint flags,
    uint accessControl,
    ref IntPtr accessControlRef);

// Almacenar contraseña con control de acceso
[DllImport("/System/Library/Frameworks/Security.framework/Security")]
private static extern int SecKeychainAddGenericPasswordWithAccess(
    IntPtr keychain,
    int serviceNameLength,
    string serviceName,
    int accountNameLength,
    string accountName,
    int passwordLength,
    string password,
    IntPtr accessControl,
    IntPtr itemRef);

// Configurar acceso en ítem existente
[DllImport("/System/Library/Frameworks/Security.framework/Security")]
private static extern int SecKeychainItemSetAccess(
    IntPtr itemRef,
    IntPtr accessControl);
```

### Constantes de Control de Acceso

```csharp
private const uint kSecAttrAccessibleWhenUnlocked = 0x0000000D;
private const uint kSecAccessControlBiometryAny = 0x00000001;
private const uint kSecAccessControlDevicePasscode = 0x00000004;
private const uint kSecAccessControlWatch = 0x00000010;
```

## 🚀 Cómo Usar

### 1. Almacenar Credenciales con Protección Biométrica

```csharp
var biometricService = new MacOSBiometricService();
var password = new SecurePasswordService().CreateSecureString("mi_contraseña");

// Esto almacena las credenciales y las protege con biometría
bool success = biometricService.StoreCredentialsWithBiometric(
    "C##IMAC", 
    password, 
    "192.168.1.100:1521/XE"
);
```

### 2. Recuperar Credenciales (Desencadena Biometría)

```csharp
// Esta llamada automáticamente mostrará el prompt de Touch ID/Face ID/Apple Watch
var credentials = biometricService.RetrieveCredentialsWithBiometric("192.168.1.100:1521/XE");

if (credentials != null)
{
    // Usuario autenticado exitosamente
    Console.WriteLine($"Usuario: {credentials.Username}");
    Console.WriteLine($"Host: {credentials.Host}");
}
else
{
    // Usuario canceló o falló la autenticación
    Console.WriteLine("Autenticación cancelada o fallida");
}
```

### 3. Verificar Disponibilidad

```csharp
if (biometricService.IsBiometricAvailable())
{
    Console.WriteLine("Biometría disponible: " + biometricService.GetAvailableBiometricTypes());
}
else
{
    Console.WriteLine("Biometría no disponible - usar métodos alternativos");
}
```

## 🔒 Flujo de Autenticación

### Almacenamiento:
1. ✅ Crear control de acceso con flags biométricos
2. ✅ Almacenar credenciales con `SecKeychainAddGenericPasswordWithAccess`
3. ✅ macOS configura automáticamente la protección

### Recuperación:
1. ✅ Llamar `SecKeychainFindGenericPassword`
2. ✅ macOS detecta que el ítem requiere autenticación
3. ✅ Sistema muestra prompt de Touch ID/Face ID/Apple Watch
4. ✅ Si autenticación exitosa, retorna credenciales
5. ✅ Si falla o cancela, retorna null

## ⚙️ Configuración del Sistema

### Touch ID (MacBook Pro con Touch Bar):
1. Preferencias del Sistema > Touch ID
2. Habilitar Touch ID para "Llavero"
3. Configurar huellas dactilares

### Apple Watch:
1. Preferencias del Sistema > Seguridad y Privacidad
2. Habilitar "Desbloquear con Apple Watch"
3. Asegurar que el Apple Watch esté desbloqueado

### Face ID (iPhone cercano):
1. Preferencias del Sistema > Seguridad y Privacidad
2. Habilitar "Permitir desbloqueo con iPhone"
3. iPhone debe estar desbloqueado y cerca

## 🛡️ Consideraciones de Seguridad

### ✅ Mejores Prácticas Implementadas:

1. **Limpieza de memoria**: Credenciales se limpian inmediatamente después de usar
2. **Manejo de errores**: Casos de cancelación y fallo manejados correctamente
3. **Validación**: Verificación de disponibilidad antes de intentar usar biometría
4. **Fallback**: Sistema de respaldo cuando biometría no está disponible

### ⚠️ Limitaciones del Sistema:

1. **No control específico**: No puedes forzar Touch ID vs Face ID vs Apple Watch
2. **Dependencia del sistema**: Requiere configuración correcta en macOS
3. **Dispositivos limitados**: Touch ID solo en MacBook Pro con Touch Bar

## 🧪 Testing

### Ejemplo de Uso Completo:

```csharp
var example = new BiometricUsageExample();

// Ejemplo completo de almacenamiento y recuperación
example.CompleteBiometricExample();

// Configurar Keychain existente
example.ConfigureExistingKeychainExample();

// Integración con sistema existente
example.IntegrationExample();

// Manejo de errores
example.ErrorHandlingExample();

// Mejores prácticas
example.SecurityBestPracticesExample();
```

### Verificación Manual:

1. **Acceso a Llaveros**: Buscar entradas de "SQLmanager"
2. **Configuración**: Verificar que "Requerir confirmación" esté marcado
3. **Prueba de acceso**: Intentar acceder para verificar que aparece el prompt biométrico

## 🔍 Debugging

### Logs de Debug:

```csharp
Console.WriteLine($"[BIOMETRIC] Storing credentials for {host} with biometric protection");
Console.WriteLine($"[BIOMETRIC] Retrieving credentials for {host} - biometric prompt should appear");
Console.WriteLine($"[ERROR] Failed to create access control: {accessResult}");
```

### Códigos de Error Comunes:

- `errSecSuccess (0)`: Operación exitosa
- `errSecAuthFailed (-25291)`: Autenticación fallida o cancelada
- `errSecItemNotFound (-25300)`: Credenciales no encontradas
- `errSecDuplicateItem (-25299)`: Ítem ya existe

## 📚 Referencias

- [Apple Security Framework Documentation](https://developer.apple.com/documentation/security)
- [Keychain Services Programming Guide](https://developer.apple.com/library/archive/documentation/Security/Conceptual/keychainServConcepts/01introduction/introduction.html)
- [Local Authentication Framework](https://developer.apple.com/documentation/localauthentication)

## 🎉 Conclusión

Esta implementación proporciona acceso nativo y seguro a las capacidades biométricas de macOS, siguiendo las mejores prácticas de seguridad y manteniendo la compatibilidad con el sistema existente. La autenticación es manejada automáticamente por macOS, garantizando la máxima seguridad y experiencia de usuario. 