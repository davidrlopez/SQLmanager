namespace SQLmanager.Services
{
    public class ConnectionParameters
    {
        public string Username { get; }
        public string Password { get; }
        public string Host { get; }
        public ConnectionState State { get; private set; }

        public ConnectionParameters(string username, string password, string host)
        {
            Username = username;
            Password = password;
            Host = host;
            State = ConnectionState.Disconnected;
        }

        public void SetState(ConnectionState newState)
        {
            State = newState;
        }
    }
}