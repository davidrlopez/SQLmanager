using System;
using System.Threading;
using System.Threading.Tasks;
using SQLmanager;
using SQLmanager.Exceptions;

namespace SQLmanager.Services
{
    public class ConnectionManager : IConnectionManager
    {
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly IAuditLogger _auditLogger;
        private string? _lastError;
        private bool _isLockAcquired = false;
        private int _connectionAttempts = 0;
        private DateTime _lastAttemptTime = DateTime.MinValue;
        
        // Rate limiting: max 5 attempts per minute
        private const int MaxAttemptsPerMinute = 5;
        private const int RateLimitWindowMinutes = 1;

        public ConnectionManager(IAuditLogger? auditLogger = null)
        {
            _auditLogger = auditLogger ?? new FileAuditLogger();
        }

        public async Task<bool> TryAcquireLock(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check rate limiting
                if (IsRateLimited())
                {
                    _auditLogger.LogError("Connection attempt blocked due to rate limiting");
                    return false;
                }

                Console.WriteLine("[DEBUG] Entering TryAcquireLock");
                var result = await _connectionLock.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                Console.WriteLine($"[DEBUG] TryAcquireLock result: {result}");
                _isLockAcquired = result;
                
                if (result)
                {
                    UpdateAttemptTracking();
                }
                
                return result;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[DEBUG] TryAcquireLock OperationCanceledException");
                _auditLogger.LogError("Connection lock acquisition cancelled");
                _isLockAcquired = false;
                return false;
            }
        }

        public void ReleaseLock()
        {
            if (_isLockAcquired)
            {
                _connectionLock.Release();
                _isLockAcquired = false;
            }
        }

        public async Task ValidateConnection(IDatabaseService databaseService, ConnectionParameters parameters)
        {
            parameters.SetState(ConnectionState.Connecting);
            
            try
            {
                var connectionString = databaseService.CreateConnectionString(new ConnectionUser(parameters));
                await databaseService.ValidateConnection(connectionString);
                parameters.SetState(ConnectionState.Connected);
                _lastError = null;
                
                _auditLogger.LogConnectionAttempt(parameters.Username, parameters.Host, true);
            }
            catch (Exception ex)
            {
                _lastError = GetSafeErrorMessage(ex);
                parameters.SetState(ConnectionState.Error);
                
                _auditLogger.LogConnectionAttempt(parameters.Username, parameters.Host, false);
                _auditLogger.LogError($"Connection validation failed: {ex.Message}");
                
                throw new ConnectionValidationException(_lastError, ex);
            }
        }

        public async Task<string> EstablishConnection(IDatabaseService databaseService, ConnectionParameters parameters)
        {
            parameters.SetState(ConnectionState.Connecting);
            
            try
            {
                var connectionString = databaseService.CreateConnectionString(new ConnectionUser(parameters));
                await databaseService.ValidateConnection(connectionString);
                parameters.SetState(ConnectionState.Connected);
                _lastError = null;
                
                _auditLogger.LogConnectionAttempt(parameters.Username, parameters.Host, true);
                return connectionString;
            }
            catch (Exception ex)
            {
                _lastError = GetSafeErrorMessage(ex);
                parameters.SetState(ConnectionState.Error);
                
                _auditLogger.LogConnectionAttempt(parameters.Username, parameters.Host, false);
                _auditLogger.LogError($"Connection establishment failed: {ex.Message}");
                
                throw new ConnectionValidationException(_lastError, ex);
            }
        }

        public string? GetLastError() => _lastError;

        private bool IsRateLimited()
        {
            var now = DateTime.Now;
            
            // Reset counter if time window has passed
            if (now.Subtract(_lastAttemptTime).TotalMinutes >= RateLimitWindowMinutes)
            {
                _connectionAttempts = 0;
            }
            
            return _connectionAttempts >= MaxAttemptsPerMinute;
        }

        private void UpdateAttemptTracking()
        {
            var now = DateTime.Now;
            
            // Reset counter if time window has passed
            if (now.Subtract(_lastAttemptTime).TotalMinutes >= RateLimitWindowMinutes)
            {
                _connectionAttempts = 0;
            }
            
            _connectionAttempts++;
            _lastAttemptTime = now;
        }

        private static string GetSafeErrorMessage(Exception ex) => ex switch
        {
            ArgumentException _ => "Invalid connection parameters provided",
            ConnectionValidationException validationEx => validationEx.Message,
            Oracle.ManagedDataAccess.Client.OracleException oracleEx => oracleEx.Number switch
            {
                1017 => "Authentication failed. Please check your credentials.",
                12154 => "Database server not found. Please verify the server address.",
                12514 => "Database service is not available.",
                12541 => "No database listener found.",
                28000 => "Account is locked. Contact your administrator.",
                _ => "Database connection failed. Please check your settings."
            },
            TimeoutException _ => "Connection timeout. Please check your network connection.",
            _ => "An unexpected error occurred during connection."
        };

        private class ConnectionUser : IUserCredentials
        {
            public string Username { get; }
            public string Host { get; }
            public string Password { get; }

            public ConnectionUser(ConnectionParameters parameters)
            {
                Username = parameters.Username ?? string.Empty;
                Host = parameters.Host ?? string.Empty;
                Password = parameters.Password ?? string.Empty;
            }
        }
    }
}