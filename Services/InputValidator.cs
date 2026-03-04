using System.Text.RegularExpressions;

namespace SQLmanager.Services
{
    public class InputValidator : IInputValidator
    {
        public bool Validate(string input)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(input) && input.Length <= 100 &&
                       !input.Contains(';') && !input.Contains('=') && !input.Contains('\0') &&
                       Regex.IsMatch(input, @"^[a-zA-Z0-9._#\-$:\[\]]+$");
            }
            catch
            {
                return false;
            }
        }

        public bool ValidateCredentials(IUserCredentials credentials)
        {
            if (credentials == null) return false;

            return !string.IsNullOrWhiteSpace(credentials.Username) &&
                   !string.IsNullOrWhiteSpace(credentials.Password) &&
                   !string.IsNullOrWhiteSpace(credentials.Host);
        }
    }
}