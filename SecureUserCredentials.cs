
using System;
using System.Security;

namespace SQLmanager
{
    public partial class SecureUserCredentials : IDisposable
    {
        public string Username { get; }
        public SecureString Password { get; }
        public string Host { get; }

        public SecureUserCredentials(string username, SecureString password, string host)
        {
            Username = username;
            Password = password;
            Host = host;
        }

        public void Dispose()
        {
            Password?.Dispose();
        }
    }

    public class SecureConnectionCredentials : IUserCredentials, IDisposable
    {
        public string Username { get; }
        public SecureString Password { get; }
        public string Host { get; }

        public SecureConnectionCredentials(string username, SecureString password, string host)
        {
            Username = username;
            Password = password;
            Host = host;
        }

        public void Dispose()
        {
            Password?.Dispose();
        }

        // Implement IUserCredentials interface
        string IUserCredentials.Password => throw new InvalidOperationException("Direct password access not allowed for security reasons. Use SecureString property instead.");
    }
}
