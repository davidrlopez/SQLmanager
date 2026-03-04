namespace SQLmanager.Services
{
    public interface IConnectionFormFactory
    {
        ConnectionForm CreateConnectionForm();
        ConnectionForm CreateConnectionForm(string username, string serverAddress, string password, string host);
    }

    public class ConnectionFormFactory : IConnectionFormFactory
    {
        public ConnectionForm CreateConnectionForm()
        {
            return new ConnectionForm(string.Empty, string.Empty, string.Empty, string.Empty);
        }

        public ConnectionForm CreateConnectionForm(string username, string serverAddress, string password, string host)
        {
            return new ConnectionForm(username, serverAddress, password, host);
        }
    }

    public class ConnectionForm : IConnectionFormProvider
    {
        public ConnectionForm(string username, string serverAddress, string password, string host)
        {
            Username = username ?? string.Empty;
            ServerAddress = serverAddress ?? string.Empty;
            Password = password ?? string.Empty;
            Host = host ?? string.Empty;
            Port = 0;
        }

        // Properties
        public string ServerAddress { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }

        public ConnectionForm GetConnectionForm()
        {
            return new ConnectionForm(this.Username, this.ServerAddress, this.Password, this.Host)
            {
                Port = this.Port
            };
        }
    }
}