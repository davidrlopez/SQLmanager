using System;
using System.Security;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SQLmanager.Security
{
    public class SecurePasswordService : ISecurePasswordService
    {
        public SecureString CreateSecureString(string password)
        {
            var securePassword = new SecureString();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }
            securePassword.MakeReadOnly();
            return securePassword;
        }
    }
    
    public class CredentialValidator : ICredentialValidator
    {
        private const int MinPasswordLength = 1; // Cambiado de 6 a 1 para permitir contraseñas cortas
        private const int MaxPasswordLength = 128;
        private const int MinUsernameLength = 1;
        private const int MaxUsernameLength = 30;
        private const int MinHostLength = 1;
        private const int MaxHostLength = 255;

        public bool AreCredentialsValid(SecureUserCredentials credentials)
        {
            if (credentials == null)
                return false;

            if (string.IsNullOrWhiteSpace(credentials.Username) ||
                credentials.Username.Length < MinUsernameLength ||
                credentials.Username.Length > MaxUsernameLength)
                return false;

            if (string.IsNullOrWhiteSpace(credentials.Host) ||
                credentials.Host.Length < MinHostLength ||
                credentials.Host.Length > MaxHostLength)
                return false;

            if (credentials.Password == null)
                return false;

            if (credentials.Password.Length < MinPasswordLength ||
                credentials.Password.Length > MaxPasswordLength)
                return false;

            // Hacer la validación de formato más flexible
            if (!IsValidUsernameFormat(credentials.Username))
                return false;

            if (!IsValidHostFormat(credentials.Host))
                return false;

            return true;
        }

        private static bool IsValidUsernameFormat(string username)
        {
            // Permitir más caracteres para usernames de Oracle
            return Regex.IsMatch(username, @"^[a-zA-Z0-9._#$]+$");
        }

        private static bool IsValidHostFormat(string host)
        {
            // Permitir IPs, hostnames, y puertos
            return Regex.IsMatch(host, @"^[a-zA-Z0-9.-:]+$");
        }
    }

    public static class SecureStringHelper
    {
        public static string SecureStringToString(SecureString secureString)
        {
            if (secureString == null)
                return string.Empty;

            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return Marshal.PtrToStringUni(unmanagedString) ?? string.Empty;
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}