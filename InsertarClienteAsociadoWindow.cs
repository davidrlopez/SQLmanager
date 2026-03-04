using Avalonia;
using Avalonia.Controls;
using System;
using SQLmanager.Data; // Required for OracleDao
using System.Collections.Generic;

namespace SQLmanager
{
    public class InsertarClienteAsociadoWindow : Window
    {
        public event Action? ClienteAsociadoInsertado;

        public InsertarClienteAsociadoWindow(OracleDao dao, Window parent)
        {
            Title = "Insertar Cliente Asociado";
            Width = 500;
            Height = 700;

            var idClienteAsociadoBox = new TextBox { Watermark = "ID Cliente Asociado (obligatorio)" };
            var idDistribuidorBox = new TextBox { Watermark = "ID Distribuidor" };
            var nombreClienteAsociadoBox = new TextBox { Watermark = "Nombre Cliente Asociado" };
            var nombreContactoAsociadoBox = new TextBox { Watermark = "Nombre Contacto Asociado" };
            var apellidoContactoAsociadoBox = new TextBox { Watermark = "Apellido Contacto Asociado" };
            var telefonoAsociadoBox = new TextBox { Watermark = "Teléfono Asociado" };
            var lineaDireccion1Box = new TextBox { Watermark = "Dirección 1" };
            var lineaDireccion2Box = new TextBox { Watermark = "Dirección 2" };
            var ciudadAsociadaBox = new TextBox { Watermark = "Ciudad" };
            var regionAsociadaBox = new TextBox { Watermark = "Región" };
            var paisAsociadoBox = new TextBox { Watermark = "País" };
            var codigoPostalAsociadoBox = new TextBox { Watermark = "Código Postal" };
            var errorText = new TextBlock { Foreground = Avalonia.Media.Brushes.Red, IsVisible = false };
            var button = new Button { Content = "Insertar", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

            button.Click += (s, e) =>
            {
                errorText.IsVisible = false;
                idClienteAsociadoBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                bool valid = true;
                if (string.IsNullOrWhiteSpace(idClienteAsociadoBox.Text)) { idClienteAsociadoBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                if (!valid)
                {
                    errorText.Text = "El campo ID Cliente Asociado es obligatorio.";
                    errorText.IsVisible = true;
                    return;
                }
                // Insert into database
                try
                {
                    string sql = "INSERT INTO CLIENTE_ASOCIADO (ID_CLIENTE_ASOCIADO, ID_DISTRIBUIDOR, NOMBRE_CLIENTE_ASOCIADO, NOMBRE_CONTACTO_ASOCIADO, APELLIDO_CONTACTO_ASOCIADO, TELEFONO_ASOCIADO, LINEA_DIRECCION1_ASOCIADA, LINEA_DIRECCION2_ASOCIADA, CIUDAD_ASOCIADA, REGION_ASOCIADA, PAIS_ASOCIADO, CODIGO_POSTAL_ASOCIADO) VALUES (:idClienteAsociado, :idDistribuidor, :nombreClienteAsociado, :nombreContactoAsociado, :apellidoContactoAsociado, :telefonoAsociado, :lineaDireccion1, :lineaDireccion2, :ciudadAsociada, :regionAsociada, :paisAsociado, :codigoPostalAsociado)";
                    var parameters = new Dictionary<string, object>
                    {
                        ["idClienteAsociado"] = idClienteAsociadoBox.Text?.Trim() ?? "",
                        ["idDistribuidor"] = string.IsNullOrWhiteSpace(idDistribuidorBox.Text) ? (object)DBNull.Value : idDistribuidorBox.Text.Trim(),
                        ["nombreClienteAsociado"] = string.IsNullOrWhiteSpace(nombreClienteAsociadoBox.Text) ? (object)DBNull.Value : nombreClienteAsociadoBox.Text.Trim(),
                        ["nombreContactoAsociado"] = string.IsNullOrWhiteSpace(nombreContactoAsociadoBox.Text) ? (object)DBNull.Value : nombreContactoAsociadoBox.Text.Trim(),
                        ["apellidoContactoAsociado"] = string.IsNullOrWhiteSpace(apellidoContactoAsociadoBox.Text) ? (object)DBNull.Value : apellidoContactoAsociadoBox.Text.Trim(),
                        ["telefonoAsociado"] = string.IsNullOrWhiteSpace(telefonoAsociadoBox.Text) ? (object)DBNull.Value : telefonoAsociadoBox.Text.Trim(),
                        ["lineaDireccion1"] = string.IsNullOrWhiteSpace(lineaDireccion1Box.Text) ? (object)DBNull.Value : lineaDireccion1Box.Text.Trim(),
                        ["lineaDireccion2"] = string.IsNullOrWhiteSpace(lineaDireccion2Box.Text) ? (object)DBNull.Value : lineaDireccion2Box.Text.Trim(),
                        ["ciudadAsociada"] = string.IsNullOrWhiteSpace(ciudadAsociadaBox.Text) ? (object)DBNull.Value : ciudadAsociadaBox.Text.Trim(),
                        ["regionAsociada"] = string.IsNullOrWhiteSpace(regionAsociadaBox.Text) ? (object)DBNull.Value : regionAsociadaBox.Text.Trim(),
                        ["paisAsociado"] = string.IsNullOrWhiteSpace(paisAsociadoBox.Text) ? (object)DBNull.Value : paisAsociadoBox.Text.Trim(),
                        ["codigoPostalAsociado"] = string.IsNullOrWhiteSpace(codigoPostalAsociadoBox.Text) ? (object)DBNull.Value : codigoPostalAsociadoBox.Text.Trim(),
                    };
                    dao.ExecuteNonQuery(sql, parameters);
                    ClienteAsociadoInsertado?.Invoke();
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
                    new TextBlock { Text = "Insertar Cliente Asociado", FontWeight = Avalonia.Media.FontWeight.Bold, FontSize = 18, Margin = new Thickness(0,0,0,10) },
                    idClienteAsociadoBox,
                    idDistribuidorBox,
                    nombreClienteAsociadoBox,
                    nombreContactoAsociadoBox,
                    apellidoContactoAsociadoBox,
                    telefonoAsociadoBox,
                    lineaDireccion1Box,
                    lineaDireccion2Box,
                    ciudadAsociadaBox,
                    regionAsociadaBox,
                    paisAsociadoBox,
                    codigoPostalAsociadoBox,
                    errorText,
                    button
                }
            };
        }
    }
} 
