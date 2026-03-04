using System;
using System.Data;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using SQLmanager.Services;

namespace SQLmanager.Data
{
    public class OracleDao
    {
        public string _connectionString { get; }
        private readonly IAuditLogger _auditLogger;
        
        // Maximum number of parameters to prevent resource exhaustion
        private const int MaxParameterCount = 100;
        private const int MaxParameterValueLength = 4000; // Oracle VARCHAR2 limit

        public OracleDao(string connectionString, IAuditLogger? auditLogger = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
                
            _connectionString = connectionString;
            _auditLogger = auditLogger ?? new FileAuditLogger();
        }

        public DataTable ExecuteQuery(string sql, Dictionary<string, object> parametros)
        {
            // Validate inputs
            ValidateQueryInputs(sql, parametros);
            
            try
            {
                using var conn = new OracleConnection(_connectionString);
                using var cmd = new OracleCommand(sql, conn);

                // Set command timeout
                cmd.CommandTimeout = 30;

                AddParametersToCommand(cmd, parametros);

                using var da = new OracleDataAdapter(cmd);
                var dt = new DataTable();
                
                conn.Open();
                da.Fill(dt);
                
                // Log successful query execution (without sensitive data)
                _auditLogger.LogConnectionAttempt("query_execution", GetHostFromConnectionString(), true);
                
                return dt;
            }
            catch (Exception ex)
            {
                _auditLogger.LogError($"Query execution failed: {ex.Message}");
                _auditLogger.LogConnectionAttempt("query_execution", GetHostFromConnectionString(), false);
                throw new DatabaseOperationException("Query execution failed", ex);
            }
        }

        public int ExecuteNonQuery(string sql, Dictionary<string, object> parametros)
        {
            // Validate inputs
            ValidateQueryInputs(sql, parametros);
            
            try
            {
                using var conn = new OracleConnection(_connectionString);
                using var cmd = new OracleCommand(sql, conn);

                // Set command timeout
                cmd.CommandTimeout = 30;

                AddParametersToCommand(cmd, parametros);

                conn.Open();
                var result = cmd.ExecuteNonQuery();
                
                // Log successful non-query execution
                _auditLogger.LogConnectionAttempt("nonquery_execution", GetHostFromConnectionString(), true);
                
                return result;
            }
            catch (Exception ex)
            {
                _auditLogger.LogError($"Non-query execution failed: {ex.Message}");
                _auditLogger.LogConnectionAttempt("nonquery_execution", GetHostFromConnectionString(), false);
                throw new DatabaseOperationException("Non-query execution failed", ex);
            }
        }

        private void ValidateQueryInputs(string sql, Dictionary<string, object> parametros)
        {
            // Validate SQL
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL query cannot be null or empty");

            if (sql.Length > 32000) // Reasonable limit for SQL queries
                throw new ArgumentException("SQL query is too long");

            // Basic SQL injection prevention - look for dangerous patterns
            if (ContainsDangerousPatterns(sql))
                throw new ArgumentException("SQL query contains potentially dangerous patterns");

            // Validate parameters
            if (parametros == null)
                throw new ArgumentNullException(nameof(parametros));

            if (parametros.Count > MaxParameterCount)
                throw new ArgumentException($"Too many parameters. Maximum allowed: {MaxParameterCount}");

            foreach (var kvp in parametros)
            {
                ValidateParameter(kvp.Key, kvp.Value);
            }
        }

        private static bool ContainsDangerousPatterns(string sql)
        {
            // Basic check for dangerous SQL patterns
            // This is not comprehensive but catches common injection attempts
            var dangerousPatterns = new[]
            {
                @";\s*(DROP|DELETE|TRUNCATE|ALTER|CREATE)\s+",
                @"UNION\s+ALL\s+SELECT",
                @"--\s*$",
                @"/\*.*\*/",
                @"xp_cmdshell",
                @"sp_executesql"
            };

            foreach (var pattern in dangerousPatterns)
            {
                if (Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
                    return true;
            }

            return false;
        }

        private static void ValidateParameter(string key, object value)
        {
            // Validate parameter name
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Parameter name cannot be null or empty");

            if (!Regex.IsMatch(key, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                throw new ArgumentException($"Invalid parameter name: {key}");

            // Validate parameter value
            if (value != null)
            {
                var valueStr = value.ToString();
                if (valueStr != null && valueStr.Length > MaxParameterValueLength)
                    throw new ArgumentException($"Parameter value too long for parameter: {key}");
            }
        }

        private void AddParametersToCommand(OracleCommand cmd, Dictionary<string, object> parametros)
        {
            foreach (var kvp in parametros)
            {
                var param = new OracleParameter(kvp.Key, kvp.Value ?? DBNull.Value);

                // Set appropriate Oracle data type based on value type
                if (kvp.Value is int)
                    param.OracleDbType = OracleDbType.Int32;
                else if (kvp.Value is long)
                    param.OracleDbType = OracleDbType.Int64;
                else if (kvp.Value is decimal || kvp.Value is double || kvp.Value is float)
                    param.OracleDbType = OracleDbType.Decimal;
                else if (kvp.Value is DateTime)
                    param.OracleDbType = OracleDbType.Date;
                else if (kvp.Value is bool)
                    param.OracleDbType = OracleDbType.Int32; // Oracle doesn't have boolean, use 0/1
                else if (kvp.Value == null)
                {
                    param.Value = DBNull.Value;
                    param.OracleDbType = OracleDbType.Varchar2;
                }
                else // Default to Varchar2 for strings and other types
                {
                    param.OracleDbType = OracleDbType.Varchar2;
                    // Set size for VARCHAR2 parameters to prevent issues
                    param.Size = Math.Min(kvp.Value.ToString()?.Length ?? 0, 4000);
                }

                cmd.Parameters.Add(param);
            }
        }

        private string GetHostFromConnectionString()
        {
            try
            {
                var match = Regex.Match(_connectionString, @"Data Source=([^;]+)", RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }

    // Custom exception for database operations
    public class DatabaseOperationException : Exception
    {
        public DatabaseOperationException(string message) : base(message)
        {
        }

        public DatabaseOperationException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}