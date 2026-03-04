using Avalonia.Controls;
using Avalonia.Interactivity;
using SQLmanager.Data; // Required for OracleDao
using System;
using System.Collections.Generic;
using Oracle.ManagedDataAccess.Client;
using Avalonia.Markup.Xaml;

namespace SQLmanager
{
    public partial class InsertarClienteWindow : Window
    {
        private  OracleDao? _dao;
        private  ConsultaWindow? _consultaWindow;

        // Constructor requerido por Avalonia (sin parámetros)
        public InsertarClienteWindow()
        {
            InitializeComponent();
        }

        public InsertarClienteWindow(OracleDao dao, ConsultaWindow owner) : this()
        {
            _dao = dao;
            _consultaWindow = owner;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this); // iniciador xml para la ui
            
        }

        private void InsertarClienteFin_Click(object? sender, RoutedEventArgs e)
        {
            var idClienteInput = this.FindControl<TextBox>("ID_CLIENTEInput");
            var nombreClienteInput = this.FindControl<TextBox>("NOMBRE_CLIENTEInput");
            var nombreContactoInput = this.FindControl<TextBox>("NOMBRE_CONTACTOInput");
            var apellidoContactoInput = this.FindControl<TextBox>("APELLIDO_CONTACTOInput");
            var telefonoInput = this.FindControl<TextBox>("TELEFONOInput");
            var linea1Input = this.FindControl<TextBox>("LINEA_DIRECCION1Input");
            var linea2Input = this.FindControl<TextBox>("LINEA_DIRECCION2Input");
            var ciudadInput = this.FindControl<TextBox>("CIUDADInput");
            var regionInput = this.FindControl<TextBox>("REGIONInput");
            var paisInput = this.FindControl<TextBox>("PAISInput");
            var codigoPostalInput = this.FindControl<TextBox>("CODIGO_POSTALInput");
            //var viabilidadInput = this.FindControl<TextBox>("VIABILIDADInput");
            //var viabHumanoInput = this.FindControl<TextBox>("VIAB_HUMANOInput");
            //var viabVeterinarioInput = this.FindControl<TextBox>("VIAB_VETERINARIOInput");
            //var factHInput = this.FindControl<TextBox>("FACT_HInput");
            //var afinHInput = this.FindControl<TextBox>("AFIN_HInput");
            //var puntuacHInput = this.FindControl<TextBox>("PUNTUAC_HInput");
            //var factVInput = this.FindControl<TextBox>("FACT_VInput");
            //var afinVInput = this.FindControl<TextBox>("AFIN_VInput");
            //var puntuacVInput = this.FindControl<TextBox>("PUNTUAC_VInput");
            //var puestoInput = this.FindControl<TextBox>("PUESTOInput");
            var campoInput = this.FindControl<TextBox>("CAMPOInput");
            var emailInput = this.FindControl<TextBox>("EMAILInput");
            var verificadoInput = this.FindControl<TextBox>("VERIFICADOInput");
            var comercialInput = this.FindControl<TextBox>("COMERCIALInput");
            var respuestaInput = this.FindControl<TextBox>("RESPUESTAInput");
            var categoriaInput = this.FindControl<ComboBox>("CATEGORIAInput");
            var comentariosInput = this.FindControl<TextBox>("COMENTARIOSInput");
            var mensajeErrorText = this.FindControl<TextBlock>("MensajeErrorText");

            // Comprobaciones explícitas de null
            if (_dao == null)
            {
                if (mensajeErrorText != null) { mensajeErrorText.Text = "Error interno: conexión a base de datos no inicializada (_dao es null)."; mensajeErrorText.IsVisible = true; }
                return;
            }
            if (_consultaWindow == null)
            {
                if (mensajeErrorText != null) { mensajeErrorText.Text = "Error interno: ventana principal no inicializada (_consultaWindow es null)."; mensajeErrorText.IsVisible = true; }
                return;
            }
            if (idClienteInput == null || nombreClienteInput == null || telefonoInput == null || linea1Input == null || ciudadInput == null)
            {
                if (mensajeErrorText != null) { mensajeErrorText.Text = "Error interno: no se encontraron los controles de campos obligatorios. Revisa los nombres en el XAML."; mensajeErrorText.IsVisible = true; }
                return;
            }

            // Validar campos obligatorios visualmente
            bool valid = true;
            void MarkInvalid(TextBox? tb)
            {
                if (tb != null) tb.BorderBrush = Avalonia.Media.Brushes.Red;
            }
            void MarkValid(TextBox? tb)
            {
                if (tb != null) tb.BorderBrush = Avalonia.Media.Brushes.Gray;
            }
            // Reset all
            MarkValid(idClienteInput); MarkValid(nombreClienteInput); MarkValid(telefonoInput); MarkValid(linea1Input); MarkValid(ciudadInput);
            // Check required
            if (string.IsNullOrWhiteSpace(idClienteInput?.Text)) { MarkInvalid(idClienteInput); valid = false; }
            if (string.IsNullOrWhiteSpace(nombreClienteInput?.Text)) { MarkInvalid(nombreClienteInput); valid = false; }
            if (string.IsNullOrWhiteSpace(telefonoInput?.Text)) { MarkInvalid(telefonoInput); valid = false; }
            if (string.IsNullOrWhiteSpace(linea1Input?.Text)) { MarkInvalid(linea1Input); valid = false; }
            if (string.IsNullOrWhiteSpace(ciudadInput?.Text)) { MarkInvalid(ciudadInput); valid = false; }
            if (!valid)
            {
                if (mensajeErrorText != null)
                {
                    mensajeErrorText.Text = "Faltan datos obligatorios. Por favor, rellene los campos en rojo.";
                    mensajeErrorText.IsVisible = true;
                }
                return;
            }
            if (mensajeErrorText != null)
            {
                mensajeErrorText.Text = "";
                mensajeErrorText.IsVisible = false;
            }

            try
            {
                string sql = @"INSERT INTO CLIENTE (
                    ID_CLIENTE, NOMBRE_CLIENTE, NOMBRE_CONTACTO, APELLIDO_CONTACTO, TELEFONO, LINEA_DIRECCION1, LINEA_DIRECCION2, CIUDAD, REGION, PAIS, CODIGO_POSTAL, CAMPO, EMAIL, COMERCIAL, RESPUESTA, CATEGORIA, COMENTARIOS
                ) VALUES (
                    :id_cliente, :nombre_cliente, :nombre_contacto, :apellido_contacto, :telefono, :linea_direccion1, :linea_direccion2, :ciudad, :region, :pais, :codigo_postal, :campo, :email, :verificado, :comercial, :respuesta, :categoria, :comentarios
                )";

                var parametros = new Dictionary<string, object>
                {
                    ["id_cliente"] = idClienteInput.Text?.Trim() ?? "",
                    ["nombre_cliente"] = nombreClienteInput.Text?.Trim() ?? "",
                    ["nombre_contacto"] = nombreContactoInput?.Text?.Trim() ?? (object)DBNull.Value,
                    ["apellido_contacto"] = apellidoContactoInput?.Text?.Trim() ?? (object)DBNull.Value,
                    ["telefono"] = string.IsNullOrWhiteSpace(telefonoInput?.Text) ? (object)DBNull.Value : (telefonoInput.Text?.Trim() ?? ""),
                    ["linea_direccion1"] = linea1Input?.Text?.Trim() ?? (object)DBNull.Value,
                    ["linea_direccion2"] = string.IsNullOrWhiteSpace(linea2Input?.Text) ? (object)DBNull.Value : (linea2Input.Text?.Trim() ?? ""),
                    ["ciudad"] = ciudadInput?.Text?.Trim() ?? (object)DBNull.Value,
                    ["region"] = string.IsNullOrWhiteSpace(regionInput?.Text) ? (object)DBNull.Value : (regionInput.Text?.Trim() ?? ""),
                    ["pais"] = string.IsNullOrWhiteSpace(paisInput?.Text) ? (object)DBNull.Value : (paisInput.Text?.Trim() ?? ""),
                    ["codigo_postal"] = string.IsNullOrWhiteSpace(codigoPostalInput?.Text) ? (object)DBNull.Value : codigoPostalInput.Text?.Trim() ?? "",
                   // ["viabilidad"] = string.IsNullOrWhiteSpace(viabilidadInput?.Text) ? (object)DBNull.Value : viabilidadInput.Text?.Trim() ?? "",
                   // ["viab_humano"] = string.IsNullOrWhiteSpace(viabHumanoInput?.Text) ? (object)DBNull.Value : viabHumanoInput.Text?.Trim() ?? "",
                   // ["viab_veterinario"] = string.IsNullOrWhiteSpace(viabVeterinarioInput?.Text) ? (object)DBNull.Value : viabVeterinarioInput.Text?.Trim() ?? "",
                   // ["fact_h"] = string.IsNullOrWhiteSpace(factHInput?.Text) ? (object)DBNull.Value : decimal.TryParse(factHInput.Text ?? "", out decimal fh) ? fh : (object)DBNull.Value,
                   // ["afin_h"] = string.IsNullOrWhiteSpace(afinHInput?.Text) ? (object)DBNull.Value : decimal.TryParse(afinHInput.Text ?? "", out decimal ah) ? ah : (object)DBNull.Value,
                   // ["puntuac_h"] = string.IsNullOrWhiteSpace(puntuacHInput?.Text) ? (object)DBNull.Value : decimal.TryParse(puntuacHInput.Text ?? "", out decimal ph) ? ph : (object)DBNull.Value,
                   // ["fact_v"] = string.IsNullOrWhiteSpace(factVInput?.Text) ? (object)DBNull.Value : decimal.TryParse(factVInput.Text ?? "", out decimal fv) ? fv : (object)DBNull.Value,
                   // ["afin_v"] = string.IsNullOrWhiteSpace(afinVInput?.Text) ? (object)DBNull.Value : decimal.TryParse(afinVInput.Text ?? "", out decimal av) ? av : (object)DBNull.Value,
                   // ["puntuac_v"] = string.IsNullOrWhiteSpace(puntuacVInput?.Text) ? (object)DBNull.Value : decimal.TryParse(puntuacVInput.Text ?? "", out decimal pv) ? pv : (object)DBNull.Value,
                   // ["puesto"] = string.IsNullOrWhiteSpace(puestoInput?.Text) ? (object)DBNull.Value : puestoInput.Text?.Trim() ?? "",
                    ["campo"] = string.IsNullOrWhiteSpace(campoInput?.Text) ? (object)DBNull.Value : campoInput.Text?.Trim() ?? "",
                    ["email"] = string.IsNullOrWhiteSpace(emailInput?.Text) ? (object)DBNull.Value : emailInput.Text?.Trim() ?? "",
                    ["verificado"] = string.IsNullOrWhiteSpace(verificadoInput?.Text) ? 0 : (verificadoInput.Text?.Trim() == "1" || verificadoInput.Text?.Trim()?.ToLower() == "true" || verificadoInput.Text?.Trim()?.ToLower() == "si") ? 1 : 0,
                    ["comercial"] = string.IsNullOrWhiteSpace(comercialInput?.Text) ? (object)DBNull.Value : comercialInput.Text?.Trim() ?? "",
                    ["respuesta"] = string.IsNullOrWhiteSpace(respuestaInput?.Text) ? (object)DBNull.Value : respuestaInput.Text?.Trim() ?? "",
                    ["categoria"] = categoriaInput?.SelectedItem == null ? (object)DBNull.Value : (categoriaInput.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "",
                    ["comentarios"] = comentariosInput?.Text == null ? (object)DBNull.Value : comentariosInput.Text.Trim(),
                };

                _dao.ExecuteQuery(sql, parametros);

                if (mensajeErrorText != null)
                {
                    mensajeErrorText.Text = "Cliente insertado correctamente.";
                    mensajeErrorText.Foreground = Avalonia.Media.Brushes.Green;
                    mensajeErrorText.IsVisible = true;
                }

                NotificarClienteInsertado();
                Close();
            }
            catch (Exception ex)
            {
                if (mensajeErrorText != null)
                {
                    mensajeErrorText.Text = $"Error al insertar: {ex.Message}";
                    mensajeErrorText.Foreground = Avalonia.Media.Brushes.Red;
                    mensajeErrorText.IsVisible = true;
                }
            }
        }

        

        private void LlamarMostrarErrorDesdeConsulta(string mensaje)
        {
            _consultaWindow?.MostrarError(mensaje);
        }

        public event Action? ClienteInsertado;

        private void NotificarClienteInsertado()
        {
            ClienteInsertado?.Invoke();
        }
    }
}
