// File: FileAuditLogger.cs

using System;
using System.IO;

namespace SQLmanager.Services
{
    public class FileAuditLogger : IAuditLogger
    {
        private readonly string _logPath;
        private readonly object _lockObject = new object();

        public FileAuditLogger(string? logPath = null)
        {
            _logPath = logPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SQLmanager", "audit.log");
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void LogConnectionAttempt(string username, string host, bool success)
        {
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [CONNECTION] {username}@{host} - {(success ? "SUCCESS" : "FAILED")}";
            WriteLog(logEntry);
        }

        public void LogError(string message)
        {
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] {message}";
            WriteLog(logEntry);
        }

        private void WriteLog(string entry)
        {
            try
            {
                lock (_lockObject)
                {
                    File.AppendAllText(_logPath, entry + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail - don't let logging errors break the application
            }
        }
    }
}