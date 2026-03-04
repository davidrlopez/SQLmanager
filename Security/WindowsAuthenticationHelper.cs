using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLmanager.Security;
using SQLmanager.Services;


namespace SQLmanager.Security
{
    public static class WindowsAuthenticationHelper
    {
        [DllImport("Credui.dll", CharSet = CharSet.Unicode)]
        private static extern uint CredUIPromptForWindowsCredentials(
            ref CREDUI_INFO notUsed,
            uint authError,
            ref uint authPackage,
            IntPtr inAuthBuffer,
            uint inAuthBufferSize,
            out IntPtr outAuthBuffer,
            out uint outAuthBufferSize,
            ref bool save,
            uint flags);

        [DllImport("Credui.dll", CharSet = CharSet.Unicode)]
        private static extern bool CredUnPackAuthenticationBuffer(
            uint dwFlags,
            IntPtr pAuthBuffer,
            uint cbAuthBuffer,
            StringBuilder pszUserName,
            ref int pcchMaxUserName,
            StringBuilder pszDomainName,
            ref int pcchMaxDomain,
            StringBuilder pszPassword,
            ref int pcchMaxPassword);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            public string pszMessageText;
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }

        public static bool PromptForWindowsCredentials(out string username, out string password)
        {
            username = null;
            password = null;

            var credui = new CREDUI_INFO
            {
                cbSize = Marshal.SizeOf(typeof(CREDUI_INFO)),
                hwndParent = IntPtr.Zero,
                pszCaptionText = "Autenticación requerida",
                pszMessageText = "Por favor, introduzca su contraseña o PIN para continuar.",
                hbmBanner = IntPtr.Zero
            };

            uint authPackage = 0;
            IntPtr outCredBuffer = IntPtr.Zero;
            uint outCredSize = 0;
            bool save = false;

            const uint CREDUIWIN_GENERIC = 0x1;

            uint result = CredUIPromptForWindowsCredentials(ref credui, 0, ref authPackage,
                IntPtr.Zero, 0, out outCredBuffer, out outCredSize, ref save, CREDUIWIN_GENERIC);

            if (result == 0) // NO_ERROR
            {
                var userNameBuilder = new StringBuilder(512);
                var domainBuilder = new StringBuilder(512);
                var passwordBuilder = new StringBuilder(512);

                int maxUserName = userNameBuilder.Capacity;
                int maxDomain = domainBuilder.Capacity;
                int maxPassword = passwordBuilder.Capacity;

                bool unpacked = CredUnPackAuthenticationBuffer(0, outCredBuffer, outCredSize,
                    userNameBuilder, ref maxUserName,
                    domainBuilder, ref maxDomain,
                    passwordBuilder, ref maxPassword);

                Marshal.FreeCoTaskMem(outCredBuffer);

                if (unpacked)
                {
                    username = userNameBuilder.ToString();
                    password = passwordBuilder.ToString();
                    return true;
                }
            }

            return false;
        }
    }
}
