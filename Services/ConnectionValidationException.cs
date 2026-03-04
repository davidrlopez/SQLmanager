using System;

namespace SQLmanager.Services
{
    public class ConnectionValidationException : Exception
    {
        public ConnectionValidationException(string message) : base(message)
        {
        }

        public ConnectionValidationException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
