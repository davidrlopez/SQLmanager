using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace SQLmanager.Security
{
    public class WindowsSecureCredentialManager
    {
        private const string AppPrefix = "SQLmanagerApp_";

        public bool StoreCredentials(string username, string password, string host)
        {
            var targetName = AppPrefix + host;
            return WindowsCredentialManager.SaveCredential(targetName, username, password);
        }

        public (string Username, string Password)? RetrieveCredentials(string host)
        {
            var targetName = AppPrefix + host;

            if (!WindowsCredentialManager.HasCredential(targetName))
                return null;

            bool authSuccess = WindowsAuthenticationHelper.PromptForWindowsCredentials(out string authUser, out string authPass);

            if (!authSuccess)
                return null;

            string password = WindowsCredentialManager.GetCredential(targetName);
            string username = authUser;

            if (password == null)
                return null;

            return (username, password);
        }
    }
}
