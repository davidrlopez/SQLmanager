using System.Security;

namespace SQLmanager.Security
{
    public interface ICredentialManager
    {
        void StoreCredentials(string username, SecureString password, string host, bool syncToICloud = true);
        SecureUserCredentials? RetrieveCredentials(string host);
        void DeleteCredentials(string host);
        bool HasStoredCredentials(string host);
        void UpdateCredentials(string username, SecureString password, string host, bool syncToICloud = true);
        System.Collections.Generic.List<string> GetStoredHosts();
        bool IsAvailable();
        string GetPlatformName();
    }
}
