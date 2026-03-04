using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Security;
using System.Collections.Generic; // Added missing import for List

namespace SQLmanager.Security
{
    public class CredentialManagerUI
    {
        private readonly ICredentialManager _credentialManager;

        public CredentialManagerUI()
        {
            _credentialManager = CredentialManagerFactory.CreateCredentialManager();
        }

        public void ShowCredentialManager(Window parent)
        {
            var window = new Window
            {
                Title = "Gestor de Credenciales",
                Width = 500,
                Height = 400,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var mainPanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15
            };

            // Título
            var titleText = new TextBlock
            {
                Text = "Gestor de Credenciales Seguras",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.Children.Add(titleText);

            // Descripción
            var descriptionText = new TextBlock
            {
                Text = $"Gestiona las credenciales almacenadas en {_credentialManager.GetPlatformName()}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            mainPanel.Children.Add(descriptionText);

            // Panel de credenciales existentes
            var existingCredentialsPanel = new StackPanel
            {
                Spacing = 10
            };

            var existingTitle = new TextBlock
            {
                Text = "Credenciales Almacenadas:",
                FontWeight = FontWeight.SemiBold
            };
            existingCredentialsPanel.Children.Add(existingTitle);

            // Lista de credenciales
            var credentialsList = new ListBox
            {
                Height = 150,
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Avalonia.Thickness(1),
                Name = "StoredHostsList"
            };

            // Panel para agregar hosts manualmente
            var manualHostPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Avalonia.Thickness(0, 5, 0, 0)
            };

            var manualHostInput = new TextBox
            {
                Watermark = "Host para verificar",
                Width = 200
            };
            manualHostPanel.Children.Add(manualHostInput);

            var checkHostButton = new Button
            {
                Content = "Verificar",
                Background = new SolidColorBrush(Colors.Orange),
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Avalonia.Thickness(10, 5)
            };
            manualHostPanel.Children.Add(checkHostButton);

            var refreshButton = new Button
            {
                Content = "Actualizar",
                Background = new SolidColorBrush(Colors.Blue),
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Avalonia.Thickness(10, 5)
            };
            manualHostPanel.Children.Add(refreshButton);

            existingCredentialsPanel.Children.Add(manualHostPanel);

            // Cargar credenciales existentes
            LoadExistingCredentials(credentialsList, window);
            existingCredentialsPanel.Children.Add(credentialsList);

            // Eventos para los botones
            checkHostButton.Click += (sender, e) =>
            {
                var host = manualHostInput.Text?.Trim();
                if (!string.IsNullOrEmpty(host))
                {
                    if (_credentialManager.HasStoredCredentials(host))
                    {
                        ShowMessage($"✅ Se encontraron credenciales para: {host}", "Credenciales encontradas", window);
                        LoadExistingCredentials(credentialsList, window); // Refresh list
                    }
                    else
                    {
                        ShowMessage($"❌ No se encontraron credenciales para: {host}", "Sin credenciales", window);
                    }
                }
            };

            refreshButton.Click += (sender, e) =>
            {
                LoadExistingCredentials(credentialsList, window);
                ShowMessage("Lista actualizada", "Actualizado", window);
            };

            mainPanel.Children.Add(existingCredentialsPanel);

            // Panel de nuevas credenciales
            var newCredentialsPanel = new StackPanel
            {
                Spacing = 10
            };

            var newTitle = new TextBlock
            {
                Text = "Nuevas Credenciales:",
                FontWeight = FontWeight.SemiBold
            };
            newCredentialsPanel.Children.Add(newTitle);

            // Campos de entrada
            var hostInput = new TextBox
            {
                Watermark = "Host/Servidor",
                Margin = new Avalonia.Thickness(0, 5, 0, 0)
            };
            newCredentialsPanel.Children.Add(hostInput);

            var usernameInput = new TextBox
            {
                Watermark = "Usuario",
                Margin = new Avalonia.Thickness(0, 5, 0, 0)
            };
            newCredentialsPanel.Children.Add(usernameInput);

            var passwordInput = new TextBox
            {
                Watermark = "Contraseña",
                PasswordChar = '*',
                Margin = new Avalonia.Thickness(0, 5, 0, 0)
            };
            newCredentialsPanel.Children.Add(passwordInput);

            // Opciones
            var optionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 15,
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };

            var iCloudCheckBox = new CheckBox
            {
                Content = "Sincronizar con iCloud",
                IsChecked = true
            };
            optionsPanel.Children.Add(iCloudCheckBox);

            newCredentialsPanel.Children.Add(optionsPanel);

            // Botones
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Margin = new Avalonia.Thickness(0, 15, 0, 0)
            };

            var saveButton = new Button
            {
                Content = "Guardar",
                Background = new SolidColorBrush(Colors.Green),
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Avalonia.Thickness(15, 5)
            };

            var loadButton = new Button
            {
                Content = "Cargar",
                Background = new SolidColorBrush(Colors.Blue),
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Avalonia.Thickness(15, 5)
            };

            var deleteButton = new Button
            {
                Content = "Eliminar",
                Background = new SolidColorBrush(Colors.Red),
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Avalonia.Thickness(15, 5)
            };

            var closeButton = new Button
            {
                Content = "Cerrar",
                Background = new SolidColorBrush(Colors.Gray),
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Avalonia.Thickness(15, 5)
            };

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(loadButton);
            buttonPanel.Children.Add(deleteButton);
            buttonPanel.Children.Add(closeButton);

            newCredentialsPanel.Children.Add(buttonPanel);
            mainPanel.Children.Add(newCredentialsPanel);

            // Eventos
            saveButton.Click += (sender, e) => SaveCredentials(hostInput, usernameInput, passwordInput, iCloudCheckBox.IsChecked ?? true, parent);
            loadButton.Click += (sender, e) => LoadCredentials(hostInput, usernameInput, passwordInput, parent);
            deleteButton.Click += (sender, e) => DeleteCredentials(hostInput, parent);
            closeButton.Click += (sender, e) => window.Close();

            window.Content = new ScrollViewer
            {
                Content = mainPanel
            };

            window.Show(parent);
        }

        private void LoadExistingCredentials(ListBox listBox, Window parent)
        {
            try
            {
                // Limpiar lista actual
                listBox.Items.Clear();
                
                var storedHosts = _credentialManager.GetStoredHosts();
                
                if (storedHosts.Count == 0)
                {
                    var noCredentialsItem = new ListBoxItem
                    {
                        Content = "No se encontraron credenciales guardadas",
                        IsEnabled = false,
                        Foreground = new SolidColorBrush(Colors.Gray)
                    };
                    listBox.Items.Add(noCredentialsItem);
                    return;
                }
                
                foreach (var host in storedHosts)
                {
                    var item = new ListBoxItem
                    {
                        Content = $"🔐 {host}",
                        Tag = host
                    };

                    item.DoubleTapped += (sender, e) =>
                    {
                        // Cargar credenciales al hacer doble clic
                        var credentials = _credentialManager.RetrieveCredentials(host);
                        if (credentials != null)
                        {
                            Console.WriteLine($"Credenciales cargadas para {host}");
                            
                            // Mostrar las credenciales en un mensaje
                            var passwordString = SecureStringHelper.SecureStringToString(credentials.Password);
                            var message = $"Host: {host}\nUsuario: {credentials.Username}\nContraseña: {new string('*', passwordString.Length)}";
                            
                            ShowMessage(message, $"Credenciales para {host}", parent);
                            
                            // Limpiar password de memoria
                            Array.Clear(passwordString.ToCharArray(), 0, passwordString.Length);
                        }
                        else
                        {
                            ShowMessage($"No se pudieron cargar las credenciales para {host}", "Error", parent);
                        }
                    };

                    listBox.Items.Add(item);
                }
                
                Console.WriteLine($"[CredentialManager] Loaded {storedHosts.Count} stored hosts");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CredentialManager] Error loading credentials: {ex.Message}");
                
                var errorItem = new ListBoxItem
                {
                    Content = $"Error al cargar credenciales: {ex.Message}",
                    IsEnabled = false,
                    Foreground = new SolidColorBrush(Colors.Red)
                };
                listBox.Items.Add(errorItem);
            }
        }

        private void SaveCredentials(TextBox hostInput, TextBox usernameInput, TextBox passwordInput, bool syncICloud, Window parent)
        {
            try
            {
                var host = hostInput.Text?.Trim();
                var username = usernameInput.Text?.Trim();
                var password = passwordInput.Text?.Trim();

                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    ShowMessage("Por favor, completa todos los campos", "Error", parent);
                    return;
                }

                var securePassword = new SecurePasswordService().CreateSecureString(password);
                _credentialManager.StoreCredentials(username, securePassword, host, syncICloud);

                ShowMessage("Credenciales guardadas exitosamente", "Éxito", parent);
                
                // Limpiar campos
                hostInput.Text = "";
                usernameInput.Text = "";
                passwordInput.Text = "";

                // Auto-refresh stored hosts list if present
                var list = parent.FindControl<ListBox>("StoredHostsList");
                if (list != null)
                {
                    LoadExistingCredentials(list, parent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CredentialManager] Error saving credentials: {ex.Message}");
                ShowMessage($"Error al guardar credenciales: {ex.Message}", "Error", parent);
            }
        }

        private void LoadCredentials(TextBox hostInput, TextBox usernameInput, TextBox passwordInput, Window parent)
        {
            try
            {
                var host = hostInput.Text?.Trim();

                if (string.IsNullOrEmpty(host))
                {
                    ShowMessage("Por favor, ingresa un host", "Error", parent);
                    return;
                }

                var credentials = _credentialManager.RetrieveCredentials(host);
                
                if (credentials != null)
                {
                    usernameInput.Text = credentials.Username;
                    
                    var passwordString = SecureStringHelper.SecureStringToString(credentials.Password);
                    passwordInput.Text = passwordString;
                    
                    // Limpiar password de memoria
                    Array.Clear(passwordString.ToCharArray(), 0, passwordString.Length);
                    
                    ShowMessage("Credenciales cargadas exitosamente", "Éxito", parent);
                }
                else
                {
                    ShowMessage("No se encontraron credenciales para este host", "Error", parent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CredentialManager] Error loading credentials: {ex.Message}");
                ShowMessage($"Error al cargar credenciales: {ex.Message}", "Error", parent);
            }
        }

        private void DeleteCredentials(TextBox hostInput, Window parent)
        {
            try
            {
                var host = hostInput.Text?.Trim();

                if (string.IsNullOrEmpty(host))
                {
                    ShowMessage("Por favor, ingresa un host para eliminar", "Error", parent);
                    return;
                }

                if (!_credentialManager.HasStoredCredentials(host))
                {
                    ShowMessage($"No se encontraron credenciales para el host: {host}", "Error", parent);
                    return;
                }

                _credentialManager.DeleteCredentials(host);
                ShowMessage($"Credenciales eliminadas exitosamente para: {host}", "Éxito", parent);
                
                // Limpiar campo de host
                hostInput.Text = "";

                // Auto-refresh stored hosts list if present
                var list = parent.FindControl<ListBox>("StoredHostsList");
                if (list != null)
                {
                    LoadExistingCredentials(list, parent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CredentialManager] Error deleting credentials: {ex.Message}");
                ShowMessage($"Error al eliminar credenciales: {ex.Message}", "Error", parent);
            }
        }

        private void ShowMessage(string message, string title, Window parent)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Avalonia.Thickness(0, 20, 0, 0)
                        }
                    }
                }
            };

            messageBox.Show(parent);
        }
    }
} 