using System;

namespace SQLmanager.Security
{
    public static class CredentialManagerFactory
    {
        public static ICredentialManager CreateCredentialManager()
        {
#if WINDOWS
            return new WindowsCredentialManager();
#elif MACOS
            return new MacOSKeychainManager();
#else
            throw new PlatformNotSupportedException("Plataforma no soportada para gestión de credenciales");
#endif
        }
    }
}
