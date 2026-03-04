using Avalonia;
using Avalonia.Controls;
using System;
using SQLmanager.Data; // Required for OracleDao
using System.Collections.Generic;

namespace SQLmanager
{
    public class InsertarEmpleadoWindow : Window
    {
        public event Action? EmpleadoInsertado;

        public InsertarEmpleadoWindow(OracleDao dao, Window parent)
        {
            Title = "Insertar Empleado";
            Width = 400;
            Height = 600;

            var codigoEmpleadoBox = new TextBox { Watermark = "CIF Empleado" };
            var nombreBox = new TextBox { Watermark = "Nombre" };
            var apellido1Box = new TextBox { Watermark = "Apellido 1" };
            var apellido2Box = new TextBox { Watermark = "Apellido 2 (opcional)" };
            var emailBox = new TextBox { Watermark = "Email" };
            var codigoOficinaBox = new TextBox { Watermark = "Código Oficina" };
            var codigoJefeBox = new TextBox { Watermark = "Código Jefe (opcional)" };
            var puestoBox = new TextBox { Watermark = "Puesto (opcional)" };
            var errorText = new TextBlock { Foreground = Avalonia.Media.Brushes.Red, IsVisible = false };
            var button = new Button { Content = "Insertar", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

            button.Click += (s, e) =>
            {
                errorText.IsVisible = false;
                // Reset borders
                codigoEmpleadoBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                nombreBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                apellido1Box.BorderBrush = Avalonia.Media.Brushes.Gray;
                emailBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                codigoOficinaBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                bool valid = true;
                if (string.IsNullOrWhiteSpace(codigoEmpleadoBox.Text)) { codigoEmpleadoBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (string.IsNullOrWhiteSpace(nombreBox.Text)) { nombreBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (string.IsNullOrWhiteSpace(apellido1Box.Text)) { apellido1Box.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (string.IsNullOrWhiteSpace(emailBox.Text)) { emailBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (string.IsNullOrWhiteSpace(codigoOficinaBox.Text)) { codigoOficinaBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (!valid)
                {
                    errorText.Text = "Todos los campos obligatorios deben estar completos.";
                    errorText.IsVisible = true;
                    return;
                }
                // Elimina la validación de int.TryParse para codigoEmpleadoBox
                int? codigoJefe = null;
                if (!string.IsNullOrWhiteSpace(codigoJefeBox.Text))
                {
                    if (!int.TryParse(codigoJefeBox.Text, out int jefe))
                    {
                        errorText.Text = "Código Jefe debe ser un número.";
                        errorText.IsVisible = true;
                        return;
                    }
                    codigoJefe = jefe;
                }
                // Insert into database
                try
                {
                    string sql = "INSERT INTO EMPLEADO (ID_EMPLEADO, NOMBRE, APELLIDO1, APELLIDO2, EMAIL, ID_JEFE, PUESTO, ID_USUARIO, CIUDAD) VALUES (:idEmpleado, :nombre, :apellido1, :apellido2, :email, :idJefe, :puesto, :idUsuario, :ciudad)";
                    var parameters = new Dictionary<string, object>
                    {
                        ["idEmpleado"] = codigoEmpleadoBox.Text?.Trim() ?? "",
                        ["nombre"] = nombreBox.Text?.Trim() ?? "",
                        ["apellido1"] = apellido1Box.Text?.Trim() ?? "",
                        ["apellido2"] = string.IsNullOrWhiteSpace(apellido2Box.Text) ? (object)DBNull.Value : apellido2Box.Text?.Trim() ?? "",
                        ["email"] = emailBox.Text?.Trim() ?? "",
                        ["idJefe"] = string.IsNullOrWhiteSpace(codigoJefeBox.Text) ? (object)DBNull.Value : codigoJefeBox.Text?.Trim() ?? "",
                        ["puesto"] = string.IsNullOrWhiteSpace(puestoBox.Text) ? (object)DBNull.Value : puestoBox.Text?.Trim() ?? "",
                        ["idUsuario"] = DBNull.Value, // Puedes ajustar si tienes el campo en la UI
                        ["ciudad"] = DBNull.Value // Puedes ajustar si tienes el campo en la UI
                    };
                    dao.ExecuteNonQuery(sql, parameters);
                    EmpleadoInsertado?.Invoke();
                    Close();
                }
                catch (Exception ex)
                {
                    errorText.Text = $"Error: {ex.Message}";
                    errorText.IsVisible = true;
                }
            };

            Content = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = "Insertar Empleado", FontWeight = Avalonia.Media.FontWeight.Bold, FontSize = 18, Margin = new Thickness(0,0,0,10) },
                    codigoEmpleadoBox,
                    nombreBox,
                    apellido1Box,
                    apellido2Box,
                    emailBox,
                    codigoOficinaBox,
                    codigoJefeBox,
                    puestoBox,
                    errorText,
                    button
                }
            };
        }
    }
} 
