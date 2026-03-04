using Avalonia;
using Avalonia.Controls;
using System;
using SQLmanager.Data; // Required for OracleDao
using System.Collections.Generic;

namespace SQLmanager
{
    public class InsertarPedidoWindow : Window
    {
        public event Action? PedidoInsertado;

        public InsertarPedidoWindow(OracleDao dao, Window parent)
        {
            Title = "Insertar Pedido";
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var now = DateTime.Now;
            string nowStr = now.ToString("yyyy-MM-dd HH:mm:ss");
            var idPedidoBox = new TextBox { Watermark = "ID Pedido (obligatorio)" };
            var fechaRegistroBox = new TextBox { Watermark = "Fecha Registro (YYYY-MM-DD HH:MM:SS)", Text = nowStr };
            var fechaPreparacionBox = new TextBox { Watermark = "Fecha Preparación (YYYY-MM-DD HH:MM:SS)", Text = nowStr };
            var fechaRecogidaBox = new TextBox { Watermark = "Fecha Recogida (YYYY-MM-DD HH:MM:SS)", Text = nowStr };
            var horaPreparacionBox = new TextBox { Watermark = "Hora Preparación (HH:MM)" };
            var horaRecogidaBox = new TextBox { Watermark = "Hora Recogida (HH:MM)" };
            var estadoBox = new TextBox { Watermark = "Estado" };
            var idClienteBox = new TextBox { Watermark = "CIF Cliente" };
            var idEmpleadoBox = new TextBox { Watermark = "ID Empleado" };
            var idEmpresaBox = new TextBox { Watermark = "ID Empresa (1 o 2)" };
            var fechaEntregaBox = new TextBox { Watermark = "Fecha Entrega (YYYY-MM-DD HH:MM:SS, opcional)", Text = nowStr };
            var comentariosBox = new TextBox { Watermark = "Comentarios (opcional)" };
            var idDistribuidorBox = new TextBox { Watermark = "ID Distribuidor (opcional)" };
            var errorText = new TextBlock { Foreground = Avalonia.Media.Brushes.Red, IsVisible = false };
            var button = new Button { Content = "Insertar", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

            button.Click += (s, e) =>
            {
                try
                {
                    // Deshabilitar el botón para evitar múltiples clics
                    button.IsEnabled = false;
                    errorText.IsVisible = false;
                    
                    // Reset borders
                    idPedidoBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    fechaRegistroBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    fechaPreparacionBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    fechaRecogidaBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    horaPreparacionBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    horaRecogidaBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    estadoBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    idClienteBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    idEmpleadoBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    idEmpresaBox.BorderBrush = Avalonia.Media.Brushes.Gray;
                    
                    // Validación de campos obligatorios
                    bool valid = true;
                    if (string.IsNullOrWhiteSpace(idPedidoBox.Text)) { idPedidoBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    if (string.IsNullOrWhiteSpace(fechaRegistroBox.Text)) { fechaRegistroBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    if (string.IsNullOrWhiteSpace(fechaPreparacionBox.Text)) { fechaPreparacionBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    if (string.IsNullOrWhiteSpace(fechaRecogidaBox.Text)) { fechaRecogidaBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    if (string.IsNullOrWhiteSpace(horaPreparacionBox.Text)) { horaPreparacionBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    if (string.IsNullOrWhiteSpace(horaRecogidaBox.Text)) { horaRecogidaBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    if (string.IsNullOrWhiteSpace(estadoBox.Text)) { estadoBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    if (string.IsNullOrWhiteSpace(idClienteBox.Text)) { idClienteBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    if (string.IsNullOrWhiteSpace(idEmpleadoBox.Text)) { idEmpleadoBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    if (string.IsNullOrWhiteSpace(idEmpresaBox.Text)) { idEmpresaBox.BorderBrush = Avalonia.Media.Brushes.Red; valid = false; }
                    
                    if (!valid)
                    {
                        errorText.Text = "Todos los campos obligatorios deben estar completos.";
                        errorText.IsVisible = true;
                        button.IsEnabled = true;
                        return;
                    }
                    
                    // Validación específica para ID_EMPRESA
                    if (!int.TryParse(idEmpresaBox.Text?.Trim() ?? "", out int idEmpresa) || (idEmpresa != 1 && idEmpresa != 2))
                    {
                        errorText.Text = "ID Empresa debe ser 1 o 2.";
                        idEmpresaBox.BorderBrush = Avalonia.Media.Brushes.Red;
                        errorText.IsVisible = true;
                        button.IsEnabled = true;
                        return;
                    }
                    
                    // Validación de formato de fecha/hora
                    string formato = "yyyy-MM-dd HH:mm:ss";
                    DateTime temp;
                    if (!DateTime.TryParseExact(fechaRegistroBox.Text?.Trim() ?? "", formato, null, System.Globalization.DateTimeStyles.None, out temp))
                    {
                        errorText.Text = "Fecha Registro debe tener formato YYYY-MM-DD HH:MM:SS";
                        errorText.IsVisible = true;
                        button.IsEnabled = true;
                        return;
                    }
                    if (!DateTime.TryParseExact(fechaPreparacionBox.Text?.Trim() ?? "", formato, null, System.Globalization.DateTimeStyles.None, out temp))
                    {
                        errorText.Text = "Fecha Preparación debe tener formato YYYY-MM-DD HH:MM:SS";
                        errorText.IsVisible = true;
                        button.IsEnabled = true;
                        return;
                    }
                    if (!DateTime.TryParseExact(fechaRecogidaBox.Text?.Trim() ?? "", formato, null, System.Globalization.DateTimeStyles.None, out temp))
                    {
                        errorText.Text = "Fecha Recogida debe tener formato YYYY-MM-DD HH:MM:SS";
                        errorText.IsVisible = true;
                        button.IsEnabled = true;
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(fechaEntregaBox.Text) && !DateTime.TryParseExact(fechaEntregaBox.Text.Trim(), formato, null, System.Globalization.DateTimeStyles.None, out temp))
                    {
                        errorText.Text = "Fecha Entrega debe tener formato YYYY-MM-DD HH:MM:SS o estar vacía";
                        errorText.IsVisible = true;
                        button.IsEnabled = true;
                        return;
                    }
                    
                    // Validación de formato de hora
                    string formatoHora = "HH:mm";
                    if (!DateTime.TryParseExact(horaPreparacionBox.Text?.Trim() ?? "", formatoHora, null, System.Globalization.DateTimeStyles.None, out temp))
                    {
                        errorText.Text = "Hora Preparación debe tener formato HH:MM";
                        errorText.IsVisible = true;
                        button.IsEnabled = true;
                        return;
                    }
                    if (!DateTime.TryParseExact(horaRecogidaBox.Text?.Trim() ?? "", formatoHora, null, System.Globalization.DateTimeStyles.None, out temp))
                    {
                        errorText.Text = "Hora Recogida debe tener formato HH:MM";
                        errorText.IsVisible = true;
                        button.IsEnabled = true;
                        return;
                    }
                    
                    // Ejecutar la inserción directamente
                    try
                    {
                        // Construir la consulta SQL dinámicamente para manejar campos opcionales
                        var sqlFields = new List<string>
                        {
                            "ID_PEDIDO", "FECHA_REGISTRO", "FECHA_PREPARACION", "FECHA_RECOGIDA", 
                            "HORA_PREPARACION", "HORA_RECOGIDA", "ESTADO", "ID_CLIENTE", "ID_EMPLEADO", "ID_EMPRESA"
                        };
                        
                        var sqlValues = new List<string>
                        {
                            ":idPedido", "TO_TIMESTAMP(:fechaRegistro, 'YYYY-MM-DD HH24:MI:SS')", 
                            "TO_TIMESTAMP(:fechaPreparacion, 'YYYY-MM-DD HH24:MI:SS')", 
                            "TO_TIMESTAMP(:fechaRecogida, 'YYYY-MM-DD HH24:MI:SS')", 
                            ":horaPreparacion", ":horaRecogida", ":estado", ":idCliente", ":idEmpleado", ":idEmpresa"
                        };
                        
                        var parameters = new Dictionary<string, object>
                        {
                            ["idPedido"] = idPedidoBox.Text?.Trim() ?? "",
                            ["fechaRegistro"] = fechaRegistroBox.Text?.Trim() ?? "",
                            ["fechaPreparacion"] = fechaPreparacionBox.Text?.Trim() ?? "",
                            ["fechaRecogida"] = fechaRecogidaBox.Text?.Trim() ?? "",
                            ["horaPreparacion"] = horaPreparacionBox.Text?.Trim() ?? "",
                            ["horaRecogida"] = horaRecogidaBox.Text?.Trim() ?? "",
                            ["estado"] = estadoBox.Text?.Trim() ?? "",
                            ["idCliente"] = idClienteBox.Text?.Trim() ?? "",
                            ["idEmpleado"] = idEmpleadoBox.Text?.Trim() ?? "",
                            ["idEmpresa"] = idEmpresa
                        };
                        
                        // Agregar campos opcionales solo si tienen valor
                        if (!string.IsNullOrWhiteSpace(fechaEntregaBox.Text))
                        {
                            sqlFields.Add("FECHA_ENTREGA");
                            sqlValues.Add("TO_TIMESTAMP(:fechaEntrega, 'YYYY-MM-DD HH24:MI:SS')");
                            parameters["fechaEntrega"] = fechaEntregaBox.Text?.Trim() ?? "";
                        }
                        
                        if (!string.IsNullOrWhiteSpace(comentariosBox.Text))
                        {
                            sqlFields.Add("COMENTARIOS");
                            sqlValues.Add(":comentarios");
                            parameters["comentarios"] = comentariosBox.Text?.Trim() ?? "";
                        }
                        
                        if (!string.IsNullOrWhiteSpace(idDistribuidorBox.Text))
                        {
                            sqlFields.Add("ID_DISTRIBUIDOR");
                            sqlValues.Add(":idDistribuidor");
                            parameters["idDistribuidor"] = idDistribuidorBox.Text?.Trim() ?? "";
                        }
                        
                        string sql = $"INSERT INTO PEDIDO ({string.Join(", ", sqlFields)}) VALUES ({string.Join(", ", sqlValues)})";
                        
                        dao.ExecuteNonQuery(sql, parameters);
                        
                        // Si llegamos aquí, la inserción fue exitosa
                        PedidoInsertado?.Invoke();
                        Close();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error al insertar en la base de datos: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    errorText.Text = $"Error: {ex.Message}";
                    errorText.IsVisible = true;
                    button.IsEnabled = true;
                }
            };

            Content = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = "Insertar Pedido", FontWeight = Avalonia.Media.FontWeight.Bold, FontSize = 18, Margin = new Thickness(0,0,0,10) },
                    idPedidoBox,
                    fechaRegistroBox,
                    new TextBlock { Text = "Fecha de creación del pedido", FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray },
                    fechaPreparacionBox,
                    new TextBlock { Text = "Fecha de preparación", FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray },
                    fechaRecogidaBox,
                    new TextBlock { Text = "Fecha de recogida", FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray },
                    horaPreparacionBox,
                    horaRecogidaBox,
                    fechaEntregaBox,
                    new TextBlock { Text = "Fecha de entrega (opcional)", FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray },
                    estadoBox,
                    comentariosBox,
                    idClienteBox,
                    idEmpleadoBox,
                    idEmpresaBox,
                    new TextBlock { Text = "ID de la empresa (1 = SQLmanager, 2 = Rumm)", FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray },
                    idDistribuidorBox,
                    errorText,
                    button
                }
            };
        }
    }
} 
