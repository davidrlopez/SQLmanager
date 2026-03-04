using System.Threading;
using System.Threading.Tasks;
using SQLmanager.Services;

namespace SQLmanager
{
    /// <summary>
    /// Provides a design-time implementation of IConnectionManager for use in the UI designer
    /// </summary>
    public class DesignTimeConnectionManager : IConnectionManager
    {
        /// <summary>
        /// Simulates acquiring a connection lock, always returns true in design mode
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>True, indicating successful lock acquisition</returns>
        public Task<bool> TryAcquireLock(CancellationToken cancellationToken = default) => 
            Task.FromResult(true);

        /// <summary>
        /// Simulates releasing a connection lock, no-op in design mode
        /// </summary>
        public void ReleaseLock() { }

        /// <summary>
        /// Simulates connection validation, always succeeds in design mode
        /// </summary>
        /// <param name="databaseService">Database service instance</param>
        /// <param name="parameters">Connection parameters</param>
        public Task ValidateConnection(IDatabaseService databaseService, ConnectionParameters parameters) => 
            Task.CompletedTask;

        /// <summary>
        /// Simulates establishing a connection, returns empty string in design mode
        /// </summary>
        /// <param name="databaseService">Database service instance</param>
        /// <param name="parameters">Connection parameters</param>
        /// <returns>Empty string as connection identifier</returns>
        public Task<string> EstablishConnection(IDatabaseService databaseService, ConnectionParameters parameters) => 
            Task.FromResult(string.Empty);
    }
}