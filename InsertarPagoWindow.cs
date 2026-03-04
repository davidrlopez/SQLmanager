using Avalonia;
using Avalonia.Controls;
using System;
using SQLmanager.Data; // Required for OracleDao
using System.Collections.Generic;

namespace SQLmanager
{
    public class InsertarPagoWindow : Window
    {
        public event Action? PagoInsertado;

        public InsertarPagoWindow(OracleDao dao, Window parent)
        {
            Title = "Insertar Pago";
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var now = DateTime.Now;
            string nowStr = now.ToString("yyyy-MM-dd HH:mm:ss");
            
            var codigoClienteBox = new TextBox { Watermark = "CIF Cliente" };
            var idTransaccionBox = new TextBox { Watermark = "ID Transacción" };
            var formaPagoBox = new TextBox { Watermark = "Forma de Pago" };
            var fechaPagoBox = new TextBox { Watermark = "Fecha de Pago (YYYY-MM-DD HH:MM:SS)", Text = nowStr };
            var totalBox = new TextBox { Watermark = "Total" };
            var errorText = new TextBlock { Foreground = Avalonia.Media.Brushes.Red, IsVisible = false };
            var button = new Button { Content = "Insertar", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

            button.Click += (s, e) =>
            {
                errorText.IsVisible = false;
                // Reset borders
                codigoClienteBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                idTransaccionBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                formaPagoBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                fechaPagoBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                totalBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                bool valid = true;
                if (string.IsNullOrWhiteSpace(codigoClienteBox.Text)) { codigoClienteBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (string.IsNullOrWhiteSpace(idTransaccionBox.Text)) { idTransaccionBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (string.IsNullOrWhiteSpace(formaPagoBox.Text)) { formaPagoBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (string.IsNullOrWhiteSpace(fechaPagoBox.Text)) { fechaPagoBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (string.IsNullOrWhiteSpace(totalBox.Text)) { totalBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (!valid)
                {
                    errorText.Text = "Todos los campos son obligatorios.";
                    errorText.IsVisible = true;
                    return;
                }
                // Elimina la validación de int.TryParse para codigoClienteBox
                if (!decimal.TryParse(totalBox.Text, out decimal total))
                {
                    errorText.Text = "Total debe ser un número.";
                    errorText.IsVisible = true;
                    return;
                }
                // Validación de formato de fecha/hora para FECHA_PAGO
                string formato = "yyyy-MM-dd HH:mm:ss";
                DateTime temp;
                if (!DateTime.TryParseExact(fechaPagoBox.Text.Trim(), formato, null, System.Globalization.DateTimeStyles.None, out temp))
                {
                    errorText.Text = "Fecha de Pago debe tener formato YYYY-MM-DD HH:MM:SS";
                    errorText.IsVisible = true;
                    return;
                }
                // Insert into database
                try
                {
                    string sql = "INSERT INTO PAGO (ID_CLIENTE, ID_TRANSACCION, FORMA_PAGO, FECHA_PAGO, TOTAL) VALUES (:idCliente, :idTransaccion, :formaPago, TO_TIMESTAMP(:fechaPago, 'YYYY-MM-DD HH24:MI:SS'), :total)";
                    var parameters = new Dictionary<string, object>
                    {
                        ["idCliente"] = codigoClienteBox.Text?.Trim() ?? "",
                        ["idTransaccion"] = idTransaccionBox.Text?.Trim() ?? "",
                        ["formaPago"] = formaPagoBox.Text?.Trim() ?? "",
                        ["fechaPago"] = fechaPagoBox.Text?.Trim() ?? "",
                        ["total"] = total
                    };
                    dao.ExecuteNonQuery(sql, parameters);
                    PagoInsertado?.Invoke();
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
                    new TextBlock { Text = "Insertar Pago", FontWeight = Avalonia.Media.FontWeight.Bold, FontSize = 18, Margin = new Thickness(0,0,0,10) },
                    codigoClienteBox,
                    idTransaccionBox,
                    formaPagoBox,
                    fechaPagoBox,
                    new TextBlock { Text = "Fecha y hora del pago", FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray },
                    totalBox,
                    errorText,
                    button
                }
            };
        }
    }
} 
