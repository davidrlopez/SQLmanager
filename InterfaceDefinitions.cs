// InterfaceDefinitions.cs
using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using SQLmanager;
using SQLmanager.Services;

namespace SQLmanager
{
    public interface IUserCredentials
    {
        string Username { get; }
        string Host { get; }
        string Password { get; }
    }

    public interface IDatabaseService
    {
        string CreateConnectionString(IUserCredentials credentials);
        Task ValidateConnection(string connectionString);
    }

    public interface IDialogFactory
    {
        IMessageDialog CreateMessageDialog(string title, string message);
    }

    public interface IMessageDialog
    {
        void Show();
        Task ShowDialog(Window owner);
    }

    public interface IInputValidator
    {
        bool Validate(string input);
    }

    public interface ILoadingState
    {
        bool IsLoading { get; }
    }

    public interface IWindowFactory
    {
        object CreateQueryWindow(string connectionString);
    }

    public interface IConnectionFormProvider
    {
        ConnectionForm GetConnectionForm();
    }

    public interface IConnectionManager
    {
        Task<bool> TryAcquireLock(CancellationToken cancellationToken = default);
        void ReleaseLock();
        Task ValidateConnection(IDatabaseService databaseService, ConnectionParameters parameters);
        Task<string> EstablishConnection(IDatabaseService databaseService, ConnectionParameters parameters);
    }

    public interface ICredentialValidator
    {
        bool AreCredentialsValid(SecureUserCredentials credentials);
    }

    public interface ISecurePasswordService
    {
        SecureString CreateSecureString(string password);
    }

    public interface IQueryWindow
    {
        void Show();
    }

    public interface IMainWindowViewModel
    {
        IDialogFactory DialogFactory { get; }
        Task ConnectAsync(ConnectionCredentials credentials, Action onSuccess, Window parent);
        Task ConnectAsync(SecureConnectionCredentials credentials, Action onSuccess, Window parent);
    }
}