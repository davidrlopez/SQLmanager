using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using SQLmanager.Security;

namespace SQLmanager.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly IAuditLogger _auditLogger;

        public DatabaseService(IAuditLogger auditLogger)
        {
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public string CreateConnectionString(IUserCredentials credentials)
        {
            ValidateCredentials(credentials);

            var dataSource = credentials.Host.Contains(":") ? credentials.Host : $"{credentials.Host}:1521/FREE";
            
            var builder = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder
            {
                UserID = credentials.Username,
                Password = credentials.Password,
                DataSource = dataSource,
                ConnectionTimeout = 60,
                Pooling = true,
                MaxPoolSize = 10,
                MinPoolSize = 1
            };

            return builder.ToString();
        }

        public string CreateConnectionString(SecureConnectionCredentials credentials)
        {
            ValidateSecureCredentials(credentials);

            var password = SecureStringHelper.SecureStringToString(credentials.Password);
            var dataSource = credentials.Host.Contains(":") ? credentials.Host : $"{credentials.Host}:1521/FREE";
            
            var builder = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder
            {
                UserID = credentials.Username,
                Password = password,
                DataSource = dataSource,
                ConnectionTimeout = 60,
                Pooling = true,
                MaxPoolSize = 10,
                MinPoolSize = 1
            };

            var connectionString = builder.ToString();
            
            // Clear password from memory immediately
            var passwordChars = password.ToCharArray();
            Array.Clear(passwordChars, 0, passwordChars.Length);

            return connectionString;
        }

        public async Task ValidateConnection(string connectionString)
        {
            var credentials = ExtractCredentialsFromConnectionString(connectionString);
            
            try
            {
                Console.WriteLine($"[DEBUG] Attempting to connect to Oracle...");
                Console.WriteLine($"[DEBUG] Host: {credentials.host}");
                Console.WriteLine($"[DEBUG] Username: {credentials.username}");
                Console.WriteLine($"[DEBUG] Connection string (sanitized): {SanitizeConnectionString(connectionString)}");
                Console.WriteLine($"[DEBUG] Connection timeout set to 60 seconds");

                var startTime = DateTime.Now;
                using var connection = new OracleConnection(connectionString);
                
                Console.WriteLine($"[DEBUG] Opening connection at {startTime:HH:mm:ss}...");
                await connection.OpenAsync();
                var endTime = DateTime.Now;
                var duration = endTime - startTime;
                
                Console.WriteLine($"[DEBUG] Connection successful! Duration: {duration.TotalMilliseconds}ms");
                
                // Log successful connection
                _auditLogger.LogConnectionAttempt(credentials.username, credentials.host, true);
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"[DEBUG] Oracle Exception: {ex.Number} - {ex.Message}");
                Console.WriteLine($"[DEBUG] Exception details: {ex}");
                
                // Log failed connection attempt
                _auditLogger.LogConnectionAttempt(credentials.username, credentials.host, false);
                
                // Log the full error internally for debugging
                _auditLogger.LogError($"Oracle connection failed: {ex.Number} - {ex.Message}");
                
                // Throw safe error message to user
                throw new ConnectionValidationException(GetSafeErrorMessage(ex));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] General Exception: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                
                _auditLogger.LogConnectionAttempt(credentials.username, credentials.host, false);
                _auditLogger.LogError($"Unexpected connection error: {ex.Message}");
                throw new ConnectionValidationException("Connection failed. Please check your network connection and try again.");
            }
        }

        private void ValidateCredentials(IUserCredentials credentials)
        {
            if (credentials == null)
                throw new ArgumentNullException(nameof(credentials));
                
            if (string.IsNullOrWhiteSpace(credentials.Username))
                throw new ArgumentException("Username cannot be null or empty");
                
            if (string.IsNullOrWhiteSpace(credentials.Host))
                throw new ArgumentException("Host cannot be null or empty");
                
            if (string.IsNullOrWhiteSpace(credentials.Password))
                throw new ArgumentException("Password cannot be empty");

            if (!IsValidInput(credentials.Username))
                throw new ArgumentException("Invalid username format");

            if (!IsValidInput(credentials.Host))
                throw new ArgumentException("Invalid host format");
        }

        private void ValidateSecureCredentials(SecureConnectionCredentials credentials)
        {
            if (credentials == null)
                throw new ArgumentNullException(nameof(credentials));
                
            if (string.IsNullOrWhiteSpace(credentials.Username))
                throw new ArgumentException("Username cannot be null or empty");
                
            if (string.IsNullOrWhiteSpace(credentials.Host))
                throw new ArgumentException("Host cannot be null or empty");
                
            if (credentials.Password == null)
                throw new ArgumentException("Password cannot be null");
            if (credentials.Password.Length == 0)
                throw new ArgumentException("Password cannot be empty");

            if (!IsValidInput(credentials.Username))
                throw new ArgumentException("Invalid username format");

            if (!IsValidInput(credentials.Host))
                throw new ArgumentException("Invalid host format");
        }

        private static bool IsValidInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Length > 100)
                return false;

            // Check for connection string injection characters
            if (input.Contains(';') || input.Contains('=') || input.Contains('\0'))
                return false;

            // Allow alphanumeric, dots, underscores, dashes, dollar signs, colons, and brackets
            return Regex.IsMatch(input, @"^[a-zA-Z0-9._#\-$:\[\]]+$");
        }

        private static string GetSafeErrorMessage(OracleException ex) => ex.Number switch
        {
            1017 => "Authentication failed. Please check your credentials.",
            12154 => "Database server not found. Please check the server address and ensure the database is running.",
            12514 => "Database service not available. Please check if the database service is running.",
            12541 => "No listener found on the specified port. Please check the port number and ensure the listener is running.",
            28000 => "Account is locked. Please contact your administrator.",
            12505 => "TNS:listener does not currently know of SID given in connect descriptor. Please check the service name.",
            12560 => "TNS:protocol adapter error. Please check your network configuration.",
            12170 => "TNS:Connect timeout occurred. Please check your network connection and firewall settings.",
            12535 => "TNS:operation timed out. Please check your network connection and try again.",
            _ => $"Connection failed (Error {ex.Number}): {ex.Message}"
        };

        private static (string username, string host) ExtractCredentialsFromConnectionString(string connectionString)
        {
            var username = ExtractValueFromConnectionString(connectionString, "User Id");
            var host = ExtractValueFromConnectionString(connectionString, "Data Source");
            return (username ?? "unknown", host ?? "unknown");
        }

        private static string? ExtractValueFromConnectionString(string connectionString, string key)
        {
            var match = Regex.Match(connectionString, $@"{key}=([^;]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string SanitizeConnectionString(string connectionString)
        {
            // Remove password from connection string for logging
            return Regex.Replace(connectionString, @"Password=[^;]+", "Password=***");
        }
    }

    public interface IAuditLogger
    {
        void LogConnectionAttempt(string username, string host, bool success);
        void LogError(string message);
    }
}
