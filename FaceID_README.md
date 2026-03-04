# Face ID Authentication para SQLmanager

## Descripción

SQLmanager ahora incluye soporte para autenticación con Face ID usando un iPhone o iPad que esté en la misma cuenta de iCloud. Esta funcionalidad permite una autenticación biométrica segura y conveniente.

## Requisitos

### Hardware
- **Mac**: Cualquier Mac con macOS 10.15 o superior
- **iPhone/iPad**: iPhone X o posterior, o iPad Pro con Face ID
- **Cuenta de iCloud**: Ambos dispositivos deben estar en la misma cuenta de iCloud

### Software
- **macOS**: 10.15 (Catalina) o superior
- **iOS**: 13.0 o superior
- **iCloud Keychain**: Debe estar habilitado en ambos dispositivos

## Configuración

### 1. Habilitar iCloud Keychain

1. En tu Mac:
   - Ve a **Preferencias del Sistema** > **Apple ID** > **iCloud**
   - Asegúrate de que **iCloud Keychain** esté habilitado

2. En tu iPhone/iPad:
   - Ve a **Ajustes** > **[Tu Nombre]** > **iCloud**
   - Asegúrate de que **iCloud Keychain** esté habilitado

### 2. Configurar Face ID en SQLmanager

1. Abre SQLmanager
2. Haz clic en el botón **"Face ID Configuration"**
3. La aplicación verificará automáticamente:
   - Estado de iCloud Keychain
   - Dispositivos iOS disponibles
   - Disponibilidad de Face ID

4. Si todo está correcto, verás:
   - ✅ iCloud Keychain: Habilitado
   - ✅ Dispositivos iOS: X dispositivos
   - ✅ Face ID: Disponible

### 3. Configurar Opciones

En la ventana de configuración de Face ID:

- **Habilitar autenticación con Face ID**: Activa la autenticación biométrica
- **Requerir Face ID para todas las operaciones**: Hace que Face ID sea obligatorio
- **Sincronizar credenciales con iCloud**: Guarda las credenciales en iCloud Keychain

### 4. Probar Face ID

1. Haz clic en **"Probar Autenticación con Face ID"**
2. Tu iPhone/iPad mostrará un prompt de Face ID
3. Mira a la cámara para autenticarte
4. Si es exitoso, verás "✅ Autenticación con Face ID exitosa"

## Uso

### Guardar Credenciales con Face ID

1. Ve a **"Manage Credentials"**
2. Marca la casilla **"Usar Face ID para autenticación"**
3. Haz clic en **"Guardar Credenciales"**
4. Completa los campos de usuario, contraseña y host
5. Tu iPhone/iPad solicitará autenticación con Face ID
6. Las credenciales se guardarán de forma segura en iCloud Keychain

### Cargar Credenciales con Face ID

1. Ve a **"Manage Credentials"**
2. Marca la casilla **"Usar Face ID para autenticación"**
3. Haz clic en **"Cargar Credenciales"**
4. Selecciona el host de las credenciales guardadas
5. Tu iPhone/iPad solicitará autenticación con Face ID
6. Las credenciales se cargarán automáticamente en la aplicación

## Seguridad

### Características de Seguridad

- **Autenticación biométrica**: Solo tu rostro puede desbloquear las credenciales
- **iCloud Keychain**: Las credenciales se sincronizan de forma segura
- **Encriptación**: Todas las credenciales están encriptadas
- **Sin almacenamiento local**: No se guardan credenciales en texto plano

### Consideraciones de Privacidad

- Face ID funciona completamente en el dispositivo
- Los datos biométricos nunca salen del iPhone/iPad
- iCloud Keychain usa encriptación de extremo a extremo
- Apple no puede acceder a tus credenciales

## Solución de Problemas

### Face ID no está disponible

**Posibles causas:**
- iCloud Keychain no está habilitado
- No hay dispositivos iOS con Face ID en la cuenta
- El iPhone/iPad no está conectado a internet
- Face ID está deshabilitado en el dispositivo iOS

**Soluciones:**
1. Verifica que iCloud Keychain esté habilitado en ambos dispositivos
2. Asegúrate de que tu iPhone/iPad esté conectado a internet
3. Verifica que Face ID esté configurado en tu dispositivo iOS
4. Reinicia ambos dispositivos

### Error de autenticación

**Posibles causas:**
- Face ID no reconoce tu rostro
- El dispositivo iOS está bloqueado
- Problemas de conectividad

**Soluciones:**
1. Asegúrate de que tu rostro esté bien iluminado
2. Desbloquea tu iPhone/iPad manualmente
3. Verifica la conectividad de internet
4. Intenta nuevamente la autenticación

### Credenciales no se guardan

**Posibles causas:**
- Permisos denegados
- Problemas con iCloud Keychain
- Espacio insuficiente en iCloud

**Soluciones:**
1. Verifica los permisos de la aplicación
2. Comprueba el estado de iCloud Keychain
3. Verifica el espacio disponible en iCloud
4. Usa el método estándar de guardado como respaldo

## Fallback a Método Estándar

Si Face ID no está disponible o falla, SQLmanager automáticamente:

1. Muestra un mensaje de advertencia
2. Usa el método estándar de macOS Keychain
3. Permite que la aplicación funcione normalmente

## Soporte

Si tienes problemas con Face ID:

1. Verifica que cumples todos los requisitos
2. Revisa la sección de solución de problemas
3. Contacta al soporte técnico con:
   - Versión de macOS
   - Versión de iOS
   - Modelo de iPhone/iPad
   - Mensajes de error específicos

## Notas Técnicas

### APIs Utilizadas

- **Security Framework**: Para acceso a iCloud Keychain
- **Local Authentication**: Para Face ID
- **CloudKit**: Para sincronización con iCloud

### Compatibilidad

- **macOS**: 10.15+ (Catalina, Big Sur, Monterey, Ventura, Sonoma)
- **iOS**: 13.0+ (iOS 13, 14, 15, 16, 17)
- **Dispositivos**: iPhone X+, iPad Pro con Face ID

### Limitaciones

- Requiere conexión a internet para sincronización
- Solo funciona con dispositivos Apple
- Face ID debe estar configurado en el dispositivo iOS
- iCloud Keychain debe estar habilitado en ambos dispositivos 