using System;
using Avalonia.Controls;

namespace SQLmanager.Services
{
    public class WindowFactory : IWindowFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public WindowFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object CreateQueryWindow(string connectionString)
        {
            return new ConsultaWindow(connectionString);
        }
    }
}