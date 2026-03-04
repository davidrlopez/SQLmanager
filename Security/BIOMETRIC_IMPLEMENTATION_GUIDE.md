# Guía de Implementación Biométrica para macOS

## 📋 Resumen

Esta implementación sigue las mejores prácticas de Apple para autenticación biométrica usando Keychain Services y LocalAuthentication framework. Permite usar Touch ID, Face ID (via iPhone) y Apple Watch para proteger credenciales de la aplicación.

## 🏗️ Arquitectura

### Frameworks Utilizados

1. **Security.framework**: Para operaciones del Keychain
2. **LocalAuthentication.framework**: Para autenticación biométrica
3. **CoreFoundation.framework**: Para gestión de memoria

### Flujo de Autenticación

```
App → Security.framework → LocalAuthentication.framework → Secure Enclave
```

## 🔧 Configuración del Proyecto

### 1. Info.plist Configuration

Agregar la clave `NSFaceIDUsageDescription` en el archivo `Info.plist`:

```xml
<key>NSFaceIDUsageDescription</key>
<string>Esta aplicación usa Face ID para proteger tus credenciales de base de datos de forma segura.</string>
```

### 2. Permisos Requeridos

- **Keychain Access**: Para almacenar credenciales
- **Biometric Authentication**: Para Touch ID/Face ID
- **Apple Watch**: Para desbloqueo con Apple Watch

## 🚀 Uso de la API

### Almacenar Credenciales con Biometría

```csharp
var biometricService = new MacOSBiometricService();

// Verificar disponibilidad
if (biometricService.IsBiometricAvailable())
{
    var username = "usuario_db";
    var password = new SecurePasswordService().CreateSecureString("mi_password");
    var host = "192.168.1.100";
    
    var stored = biometricService.StoreCredentialsWithBiometric(username, password, host);
    
    if (stored)
    {
        Console.WriteLine("✅ Credenciales almacenadas con protección biométrica");
    }
}
```

### Recuperar Credenciales (Desencadena Biometría)

```csharp
var credentials = biometricService.RetrieveCredentialsWithBiometric("192.168.1.100");

if (credentials != null)
{
    Console.WriteLine($"Usuario: {credentials.Username}");
    Console.WriteLine($"Host: {credentials.Host}");
    // credentials.Password contiene el SecureString
}
```

### Verificar Tipos de Biometría Disponibles

```csharp
var biometricTypes = biometricService.GetAvailableBiometricTypes();
Console.WriteLine($"Biometría disponible: {biometricTypes}");
// Posibles valores: "Touch ID", "Face ID", "No disponible"
```

## 🔒 Configuración de Seguridad

### Niveles de Accesibilidad

1. **kSecAttrAccessibleWhenPasscodeSetThisDeviceOnly** (Más Restrictivo)
   - Requiere passcode configurado
   - Solo en este dispositivo
   - No se sincroniza con iCloud Keychain

2. **kSecAttrAccessibleWhenUnlocked** (Recomendado)
   - Accesible cuando el dispositivo está desbloqueado
   - Se sincroniza con iCloud Keychain

### Flags de Control de Acceso

- **kSecAccessControlUserPresence**: Requiere autenticación biométrica o passcode
- **kSecAccessControlBiometryAny**: Solo biometría (Touch ID/Face ID)
- **kSecAccessControlDevicePasscode**: Solo passcode del dispositivo
- **kSecAccessControlWatch**: Solo Apple Watch

## 🛠️ Implementación Técnica

### 1. Crear Control de Acceso

```csharp
private IntPtr CreateBiometricAccessControl()
{
    return SecAccessControlCreateWithFlags(
        IntPtr.Zero, // Default allocator
        kSecAttrAccessibleWhenPasscodeSetThisDeviceOnly,
        kSecAccessControlUserPresence,
        IntPtr.Zero); // Error out parameter
}
```

### 2. Crear Contexto de Autenticación

```csharp
private IntPtr CreateAuthenticationContext()
{
    var context = LAContextCreate();
    if (context != IntPtr.Zero)
    {
        // Configurar duración de reutilización (10 segundos)
        SetTouchIDAuthenticationAllowableReuseDuration(context, 10);
    }
    return context;
}
```

### 3. Almacenar Elemento del Keychain

```csharp
var result = SecItemAddWithAccessControl(
    serviceName,
    accountName,
    passwordString,
    accessControl,
    authContext);
```

### 4. Recuperar con Autenticación

```csharp
var result = SecItemCopyMatchingWithContext(
    serviceName,
    accountName,
    authContext,
    out var passwordData,
    out var passwordLength);
```

## 🔍 Manejo de Errores

### Códigos de Error Comunes

- **errSecSuccess (0)**: Operación exitosa
- **errSecAuthFailed (-25291)**: Autenticación fallida
- **errSecItemNotFound (-25300)**: Elemento no encontrado
- **errSecDuplicateItem (-25299)**: Elemento duplicado
- **errSecParam (-50)**: Parámetros inválidos

### Manejo de Errores

```csharp
try
{
    var credentials = biometricService.RetrieveCredentialsWithBiometric(host);
    if (credentials != null)
    {
        // Usar credenciales
    }
    else
    {
        // Manejar error de autenticación
        Console.WriteLine("Usuario canceló la autenticación o credenciales no encontradas");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## 🧪 Testing

### Verificar Disponibilidad

```csharp
var isAvailable = biometricService.IsBiometricAvailable();
var biometricTypes = biometricService.GetAvailableBiometricTypes();

Console.WriteLine($"Biometría disponible: {isAvailable}");
Console.WriteLine($"Tipos: {biometricTypes}");
```

### Probar Configuración

```csharp
var configured = biometricService.ConfigureKeychainForBiometric("test_host");
if (configured)
{
    Console.WriteLine("✅ Configuración biométrica verificada");
}
```

## 🔐 Consideraciones de Seguridad

### 1. Gestión de Memoria

- Usar `SecureString` para passwords
- Limpiar datos sensibles de memoria
- Usar `CFRelease` para liberar recursos nativos

### 2. Prompts de Usuario

- Proporcionar prompts claros y específicos
- Explicar por qué se requiere autenticación
- No incluir nombre de la app (el sistema lo agrega automáticamente)

### 3. Fallbacks

- Implementar fallback a passcode si biometría falla
- Manejar casos donde biometría no está disponible
- Proporcionar métodos alternativos de autenticación

## 📱 Compatibilidad

### Dispositivos Soportados

- **MacBook Pro con Touch Bar**: Touch ID
- **Mac con Apple Watch**: Desbloqueo con Apple Watch
- **Mac con iPhone cercano**: Face ID (requiere configuración)

### Versiones de macOS

- **macOS 10.12.1+**: Touch ID
- **macOS 10.13+**: Apple Watch unlock
- **macOS 10.14+**: Face ID con iPhone

## 🚨 Limitaciones

1. **Face ID**: Solo funciona con iPhone cercano desbloqueado
2. **Touch ID**: Solo en MacBook Pro con Touch Bar
3. **Apple Watch**: Requiere Apple Watch desbloqueado y cerca
4. **iCloud Keychain**: Algunas configuraciones no se sincronizan

## 📚 Referencias

- [Apple Keychain Services Programming Guide](https://developer.apple.com/documentation/security/keychain_services)
- [LocalAuthentication Framework](https://developer.apple.com/documentation/localauthentication)
- [Accessing Keychain Items with Face ID or Touch ID](https://developer.apple.com/documentation/security/accessing_keychain_items_with_face_id_or_touch_id)

## 🔄 Actualizaciones

Esta implementación se actualiza regularmente para seguir las mejores prácticas de Apple y mantener compatibilidad con las últimas versiones de macOS. 