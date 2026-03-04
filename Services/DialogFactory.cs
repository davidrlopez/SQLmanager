using Avalonia.Controls;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SQLmanager.Services
{
    public class DialogFactory : IDialogFactory
    {
        public IMessageDialog CreateMessageDialog(string title, string message)
        {
            // Sanitize the message to prevent any information disclosure
            var sanitizedMessage = SanitizeErrorMessage(message);
            
            return new MessageDialog(title, sanitizedMessage);
        }
        
        private static string SanitizeErrorMessage(string message)
        {
            // Remove potentially sensitive information from error messages
            if (string.IsNullOrWhiteSpace(message))
                return "An error occurred";
                
            // Remove connection strings, IP addresses, and other sensitive data
            var sanitized = Regex.Replace(message, @"Data Source=[^;]+", "Data Source=***");
            sanitized = Regex.Replace(sanitized, @"Password=[^;]+", "Password=***");
            sanitized = Regex.Replace(sanitized, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", "***.***.***");
            
            return sanitized;
        }
    }

    public class MessageDialog : Window, IMessageDialog
    {
        public MessageDialog(string title, string message)
        {
            Title = title;
            Content = new TextBlock { Text = message };
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Width = 400;
            Height = 200;
        }

        public new void Show()
        {
            base.Show();
        }

        public new async Task ShowDialog(Window owner)
        {
            await base.ShowDialog(owner);
        }
    }
}