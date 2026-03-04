using System;

namespace SQLmanager.Exceptions
{
    public class ConnectionException : Exception
    {
        public ConnectionException(string message) : base(message) { }
        public ConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ConnectionTimeoutException : ConnectionException
    {
        public ConnectionTimeoutException() : base("Connection timeout. Please try again.") { }
    }
}