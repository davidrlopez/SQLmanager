using System;
using System.Collections.Generic;
using System.Data;
using Avalonia;
using Avalonia.Layout;
using SQLmanager.Data; // Required for OracleDao
using static Avalonia.Controls.ToolTip;
using SQLmanager; // Adjust if InsertarPedidoWindow is in a different namespace
using ClosedXML.Excel;
using System.IO;
using Avalonia.Platform.Storage;
using System.Linq; // Added for .All()
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Data.Converters;
using System.Collections.ObjectModel;
using Avalonia.Controls.Templates;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.VisualTree;
using System.Text.RegularExpressions;

namespace SQLmanager
{
    public partial class ConsultaWindow : Window, IQueryWindow
    {
        private OracleDao? _dao;
        private string _selectedEntity = "Clientes";
        private DataTable? _lastDataTable; // Store last shown table for export
        private string _lastEntidad = "Clientes";
        private List<DataRow> filasFiltradas = new();
        public ObservableCollection<object> Resultados { get; } = new();
        public Avalonia.Media.IImage? BuscarIconSource { get; set; }
        private DataTable? _lastRenderedTable = null;

        // Elimino: private (int row, int col)? _celdaEditando = null;
        private void ActivarEdicionCelda(int row, int col)
        {
            // _celdaEditando = (row, col); // This line is removed as per the edit hint.
            var listBox = this.FindControl<ListBox>("ResultadosListBox");
            listBox?.InvalidateVisual();
        }

        private void DesactivarEdicionCelda()
        {
            // _celdaEditando = null; // This line is removed as per the edit hint.
            var listBox = this.FindControl<ListBox>("ResultadosListBox");
            listBox?.InvalidateVisual();
        }

        private ObservableCollection<Cliente> _clientes = new();

        public ObservableCollection<object> DummyData { get; } = new()
        {
            new { Nombre = "Juan", Email = "juan@ejemplo.com", Telefono = "123456" },
            new { Nombre = "Ana", Email = "ana@ejemplo.com", Telefono = "654321" }
        };

        private const int FilasPorPagina = 20;
        private int paginaActual = 1;
        private int totalPaginas = 1;
        private List<DataRow> clientesFiltrados = new();

        private int? _selectedClienteRow = null;
        private string? _selectedClienteId = null;

        private Timer? _autoRefreshTimer;
        private bool _isAutoRefresh = false;

        private string? _sortColumn = null;
        private bool _sortDescending = true;

        private string? _editingClienteId = null;
        private string? _editingCol = null;
        private int _editingCaret = 0;
        private string? _editingText = null;

        private static readonly HashSet<string> _camposNoEditables = new()
        {
            "ID_CLIENTE", "VERIFICADO", "VIABILIDAD", "FACT_H", "AFIN_H", "PUNTUAC_H", "FACT_V", "AFIN_V", "PUNTUAC_V",
            "FECHA_REGISTRO", "FECHA_PREPARACION", "FECHA_RECOGIDA", "FECHA_ENTREGA", "FECHA_PAGO",
            "HORA_PREPARACION", "HORA_RECOGIDA"
        };

        private static readonly HashSet<string> _tablasEditables = new(StringComparer.OrdinalIgnoreCase)
        {
            "CLIENTE", "PAGO", "EMPLEADO", "PEDIDO", "CLIENTE_ASOCIADO"
        };

        private static readonly Regex _safeSqlIdentifier = new(@"^[A-Z_][A-Z0-9_]*$", RegexOptions.Compiled);

        // Variable privada para cachear empleados
        private List<(string Id, string Nombre)>? _empleadosCache = null;
        // Cache de distribuidores (ID -> Nombre)
        private List<(string Id, string Nombre)>? _distribuidoresCache = null;

        public ConsultaWindow()
        {
            InitializeComponent();
            DataContext = this;
            var entityListBox = this.FindControl<ListBox>("EntityListBox");
            if (entityListBox != null)
                entityListBox.SelectionChanged += EntityListBox_SelectionChanged;
            var queryTypeListBox = this.FindControl<ListBox>("QueryTypeListBox");
            if (queryTypeListBox != null)
                queryTypeListBox.SelectionChanged += QueryTypeListBox_SelectionChanged;
            var insertarClienteButton = this.FindControl<Button>("InsertarClienteButton");
            if (insertarClienteButton != null)
                insertarClienteButton.Click += InsertarCliente_Click;
            var insertarPedidoButton = this.FindControl<Button>("InsertarPedidoButton");
            if (insertarPedidoButton != null)
                insertarPedidoButton.Click += InsertarPedido_Click;
            var insertarPagoButton = this.FindControl<Button>("InsertarPagoButton");
            if (insertarPagoButton != null)
                insertarPagoButton.Click += InsertarPago_Click;
            var insertarEmpleadosButton = this.FindControl<Button>("InsertarEmpleadosButton");
            if (insertarEmpleadosButton != null)
                insertarEmpleadosButton.Click += InsertarEmpleado_Click;
            // Wire up export button directly by name
            var exportButton = this.FindControl<Button>("ExportExcelButton");
            if (exportButton != null)
                exportButton.Click += ExportToExcel_Click;
            // Set initial search icon
            SetBuscarIcon("Clientes");
            // SetResultadosTemplate("Clientes"); // Comentado para usar tabla editable
            var cabeceraScroll = this.FindControl<ScrollViewer>("CabeceraScroll");
            var resultadosScroll = this.FindControl<ScrollViewer>("ResultadosScroll");
            if (cabeceraScroll != null && resultadosScroll != null)
            {
                resultadosScroll.ScrollChanged += (s, e) =>
                {
                    cabeceraScroll.Offset = new Avalonia.Vector(resultadosScroll.Offset.X, 0);
                };
            }

            // Inicializar las opciones de consulta para la entidad por defecto (Clientes)
            UpdateQueryTypesForEntity("Clientes");
            UpdateInsertButtonForEntity("Clientes");
            
            MostrarTodosClientes();
            // En el constructor, busca el botón BuscarClientesButton y asígnale el evento Click para que llame a la función de búsqueda (DatoTextBox_KeyDown_BuscarNombreId) usando Key.Enter.
            var buscarClientesButton = this.FindControl<Button>("BuscarClientesButton");
            if (buscarClientesButton != null)
            {
                buscarClientesButton.Click += (s, e) =>
                {
                    var datoTextBox = this.FindControl<TextBox>("DatoTextBox");
                    if (datoTextBox != null)
                    {
                        datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarNombreId;
                        datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarNombreId;
                        datoTextBox.Text = ""; // Limpiar el texto del textbox
                        datoTextBox.Focus(); // Enfocar el textbox
                    }
                };
            }

            var borrarClienteBtn = this.FindControl<Button>("BorrarClienteButton");
            if (borrarClienteBtn != null)
                borrarClienteBtn.Click += BorrarClienteButton_Click;

            _autoRefreshTimer = new Timer(5000);
            _autoRefreshTimer.Elapsed += (s, e) =>
            {
                if (_lastEntidad == "Clientes")
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (AnyTextBoxHasFocus()) return;
                        // Si los datos no han cambiado, no refrescar
                        if (_lastDataTable != null && _lastRenderedTable != null &&
                            _lastDataTable.Rows.Count == _lastRenderedTable.Rows.Count &&
                            _lastDataTable.Columns.Count == _lastRenderedTable.Columns.Count)
                        {
                            bool iguales = true;
                            for (int i = 0; i < _lastDataTable.Rows.Count; i++)
                            {
                                if (_lastDataTable.Rows[i]["ID_CLIENTE"]?.ToString() !=
                                    _lastRenderedTable.Rows[i]["ID_CLIENTE"]?.ToString())
                                {
                                    iguales = false;
                                    break;
                                }
                            }

                            if (iguales) return;
                        }

                        _isAutoRefresh = true;
                        MostrarTodosClientes();
                        _isAutoRefresh = false;
                    });
                }
            };
            _autoRefreshTimer.Start();
            this.Closed += (s, e) =>
            {
                _autoRefreshTimer?.Stop();
                _autoRefreshTimer?.Dispose();
            };

            var insertarClienteAsociadoButton = this.FindControl<Button>("InsertarClienteAsociadoButton");
            if (insertarClienteAsociadoButton != null)
                insertarClienteAsociadoButton.Click += InsertarClienteAsociado_Click;
        }

        public ConsultaWindow(string connectionString) : this()
        {
            _dao = new OracleDao(connectionString);
        }

        private void EntityListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var entityListBox = sender as ListBox;
            if (entityListBox?.SelectedItem is ListBoxItem item)
            {
                var entidadSeleccionada = item.Content?.ToString() ?? "Clientes";
                
                Console.WriteLine($"[DEBUG] Cambio de entidad: {entidadSeleccionada}");
                
                // 1. Limpiar completamente el estado anterior
                LimpiarEstadoAnterior();
                
                // 2. Actualizar variables de estado ANTES de cualquier consulta
                _selectedEntity = entidadSeleccionada;
                _lastEntidad = entidadSeleccionada;
                
                // 3. Configurar la interfaz para la nueva entidad
                UpdateQueryTypesForEntity(_selectedEntity);
                UpdateInsertButtonForEntity(_selectedEntity);
                SetBuscarIcon(_selectedEntity);
                
                // 4. Ejecutar "Mostrar todos" para la nueva entidad
                EjecutarMostrarTodos(_selectedEntity);
            }
        }

        private void EjecutarMostrarTodos(string entidad)
        {
            Console.WriteLine($"[DEBUG] EjecutarMostrarTodos - Entidad: {entidad}");
            
            switch (entidad)
            {
                case "Clientes": 
                    MostrarTodosClientes(); 
                    break;
                case "Clientes Asociados": 
                    MostrarTodosClientesAsociados(); 
                    break;
                case "Pedido": 
                    MostrarTodosPedidos(); 
                    break;
                case "Pago": 
                    MostrarTodosPagos(); 
                    break;
                case "Empleados": 
                    MostrarTodosEmpleados(); 
                    break;
                default:
                    Console.WriteLine($"[DEBUG] Entidad no reconocida: {entidad}");
                    break;
            }
        }

        private void EjecutarConsulta(string entidad, string tipoConsulta)
        {
            Console.WriteLine($"[DEBUG] EjecutarConsulta → entidad: {entidad} | tipo: {tipoConsulta}");
            
            switch (tipoConsulta.ToLowerInvariant())
            {
                case "mostrar todos":
                    EjecutarMostrarTodos(entidad);
                    break;
                default:
                    Console.WriteLine($"[DEBUG] Tipo de consulta no reconocido: {tipoConsulta}");
                    break;
            }
        }

        private void LimpiarEstadoAnterior()
        {
            // Limpiar variables de estado
            _lastDataTable = null;
            _lastRenderedTable = null;
            filasFiltradas.Clear();
            paginaActual = 1;
            totalPaginas = 1;
            _sortColumn = null;
            _sortDescending = true;
            _selectedClienteRow = null;
            _selectedClienteId = null;
            
            // Limpiar el panel de resultados
            var panel = this.FindControl<StackPanel>("TablaResultado");
            if (panel != null)
            {
                panel.Children.Clear();
            }
            
            // Ocultar todos los controles de entrada
            var datoTextBox = this.FindControl<TextBox>("DatoTextBox");
            var distribuidorComboBox = this.FindControl<ComboBox>("DistribuidorComboBox");
            var categoriaComboBox = this.FindControl<ComboBox>("CategoriaComboBox");
            
            if (datoTextBox != null) 
            {
                datoTextBox.IsVisible = false;
                datoTextBox.Text = "";
            }
            if (distribuidorComboBox != null) 
            {
                distribuidorComboBox.IsVisible = false;
                distribuidorComboBox.SelectedIndex = -1;
            }
            if (categoriaComboBox != null) 
            {
                categoriaComboBox.IsVisible = false;
                categoriaComboBox.SelectedIndex = -1;
            }
        }

        private void UpdateQueryTypesForEntity(string entity)
        {
            var queryTypeListBox = this.FindControl<ListBox>("QueryTypeListBox");
            if (queryTypeListBox == null) return;
            
            // Desconectar el evento temporalmente para evitar llamadas no deseadas
            queryTypeListBox.SelectionChanged -= QueryTypeListBox_SelectionChanged;
            
            // Limpiar completamente el estado anterior del ListBox
            queryTypeListBox.ItemsSource = null;
            queryTypeListBox.Items.Clear();
            queryTypeListBox.SelectedIndex = -1;
            
            switch (entity)
            {
                case "Clientes":
                    queryTypeListBox.ItemsSource = new[]
                    {
                        "Mostrar todos", "Buscar por nombre o ID", "Buscar por categoria"
                    };
                    break;
                case "Clientes Asociados":
                    queryTypeListBox.ItemsSource = new[]
                    {
                        "Mostrar todos", "Buscar por ID o nombre", "Buscar por distribuidor"
                    };
                    break;
                case "Pedido":
                    queryTypeListBox.ItemsSource = new[] { "Mostrar todos", "Buscar por ID", "Buscar por cliente", "Rumm", "SQLmanager" };
                    break;
                case "Pago":
                    queryTypeListBox.ItemsSource = new[] { "Mostrar todos", "Buscar por ID", "Buscar por cliente" };
                    break;
                case "Empleados":
                    queryTypeListBox.ItemsSource = new[] { "Mostrar todos", "Buscar por ID", "Buscar por nombre" };
                    break;
                default:
                    queryTypeListBox.ItemsSource = new[] { "Mostrar todos" };
                    break;
            }

            // Establecer la primera opción como seleccionada
            queryTypeListBox.SelectedIndex = 0;
            
            // Reconectar el evento
            queryTypeListBox.SelectionChanged += QueryTypeListBox_SelectionChanged;
        }

        private void UpdateInsertButtonForEntity(string entity)
        {
            var insertarClienteButton = this.FindControl<Button>("InsertarClienteButton");
            var insertarPedidoButton = this.FindControl<Button>("InsertarPedidoButton");
            var insertarPagoButton = this.FindControl<Button>("InsertarPagoButton");
            var insertarEmpleadosButton = this.FindControl<Button>("InsertarEmpleadosButton");
            if (insertarClienteButton != null) insertarClienteButton.IsVisible = entity == "Clientes";
            if (insertarPedidoButton != null) insertarPedidoButton.IsVisible = entity == "Pedido";
            if (insertarPagoButton != null) insertarPagoButton.IsVisible = entity == "Pago";
            if (insertarEmpleadosButton != null) insertarEmpleadosButton.IsVisible = entity == "Empleados";
            var insertarClienteAsociadoButton = this.FindControl<Button>("InsertarClienteAsociadoButton");
            if (insertarClienteAsociadoButton != null)
                insertarClienteAsociadoButton.IsVisible = entity == "Clientes Asociados";
            
            // Mostrar botón de refrescar solo para Clientes
            ShowRefreshButton(entity == "Clientes");
        }

        private void QueryTypeListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            var panel = this.FindControl<StackPanel>("TablaResultado");
            var datoTextBox = this.FindControl<TextBox>("DatoTextBox");
            var distribuidorComboBox = this.FindControl<ComboBox>("DistribuidorComboBox");
            var categoriaComboBox = this.FindControl<ComboBox>("CategoriaComboBox");

            // 🔧 SOLUCIÓN: Obtener la entidad directamente del control EntityListBox
            var entityListBox = this.FindControl<ListBox>("EntityListBox");
            var entidadActual = "";
            if (entityListBox?.SelectedItem is ListBoxItem item)
            {
                entidadActual = item.Content?.ToString() ?? "";
            }
            
            var tipoConsulta = listBox?.SelectedIndex ?? 0;
            
            Console.WriteLine($"[DEBUG] Tipo consulta cambiado → entidad actual seleccionada: {entidadActual} (_selectedEntity = {_selectedEntity})");
            Console.WriteLine($"[DEBUG] QueryTypeListBox_SelectionChanged - Entidad: {entidadActual} | Tipo: {tipoConsulta}");

            // Sincronizar _selectedEntity con la entidad actual seleccionada
            _selectedEntity = entidadActual;

            // Hide all input fields by default
            if (datoTextBox != null) datoTextBox.IsVisible = false;
            if (distribuidorComboBox != null) distribuidorComboBox.IsVisible = false;
            if (categoriaComboBox != null) categoriaComboBox.IsVisible = false;
            
            // Solo limpiar el panel si no es "Mostrar todos" (caso 0)
            if (listBox?.SelectedIndex != 0 && panel != null)
            {
                panel.Children.Clear();
            }

            // Actualizar botones de inserción usando la entidad actual
            UpdateInsertButtonForEntity(entidadActual);

            if (listBox != null)
            {
                switch (entidadActual)
                {
                    case "Clientes":
                        switch (listBox.SelectedIndex)
                        {
                            case 0: 
                                EjecutarConsulta(entidadActual, "Mostrar todos"); 
                                break;
                            case 1:
                                if (datoTextBox != null)
                                {
                                    datoTextBox.IsVisible = true;
                                    datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarNombreId;
                                    datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarNombreId;
                                }
                                break;
                            case 2:
                                if (categoriaComboBox != null)
                                {
                                    categoriaComboBox.IsVisible = true;
                                    var categorias = new[]
                                    {
                                        "Distribución Farmacéutica", "Veterinaria", "Distribución Veterinaria",
                                        "Farmacéutica"
                                    };
                                    categoriaComboBox.ItemsSource = categorias;
                                    categoriaComboBox.SelectionChanged -= CategoriaComboBox_SelectionChanged;
                                    categoriaComboBox.SelectionChanged += CategoriaComboBox_SelectionChanged;
                                    categoriaComboBox.SelectedIndex = -1; // Fuerza a que el usuario seleccione
                                }
                                break;
                        }
                        break;
                    case "Clientes Asociados":
                        switch (listBox.SelectedIndex)
                        {
                            case 0: 
                                EjecutarConsulta(entidadActual, "Mostrar todos"); 
                                break;
                            case 1: // Buscar por ID
                                if (datoTextBox != null)
                                {
                                    datoTextBox.IsVisible = true;
                                    datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarClienteAsociadoId;
                                    datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarClienteAsociadoId;
                                }
                                break;
                            case 2: // Buscar por distribuidor
                                if (datoTextBox != null)
                                {
                                    datoTextBox.IsVisible = true;
                                    datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarClienteAsociadoDistribuidor;
                                    datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarClienteAsociadoDistribuidor;
                                }
                                break;
                        }
                        break;
                    case "Pedido":
                        switch (listBox.SelectedIndex)
                        {
                            case 0: 
                                EjecutarConsulta(entidadActual, "Mostrar todos"); 
                                break;
                            case 1: // Buscar por ID
                                if (datoTextBox != null)
                                {
                                    datoTextBox.IsVisible = true;
                                    datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarPedidoId;
                                    datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarPedidoId;
                                }
                                break;
                            case 2: // Buscar por cliente
                                if (datoTextBox != null)
                                {
                                    datoTextBox.IsVisible = true;
                                    datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarPedidoCliente;
                                    datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarPedidoCliente;
                                }
                                break;
                            case 3: // Rumm
                                MostrarPedidosRumm();
                                break;
                            case 4: // SQLmanager
                                MostrarPedidosSQLmanager();
                                break;
                        }
                        break;
                    case "Pago":
                        switch (listBox.SelectedIndex)
                        {
                            case 0: 
                                EjecutarConsulta(entidadActual, "Mostrar todos"); 
                                break;
                            case 1: // Buscar por ID
                                if (datoTextBox != null)
                                {
                                    datoTextBox.IsVisible = true;
                                    datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarPagoId;
                                    datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarPagoId;
                                }
                                break;
                            case 2: // Buscar por cliente
                                if (datoTextBox != null)
                                {
                                    datoTextBox.IsVisible = true;
                                    datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarPagoCliente;
                                    datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarPagoCliente;
                                }
                                break;
                        }
                        break;
                    case "Empleados":
                        switch (listBox.SelectedIndex)
                        {
                            case 0: 
                                EjecutarConsulta(entidadActual, "Mostrar todos"); 
                                break;
                            case 1: // Buscar por ID
                                if (datoTextBox != null)
                                {
                                    datoTextBox.IsVisible = true;
                                    datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarEmpleadoId;
                                    datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarEmpleadoId;
                                }
                                break;
                            case 2: // Buscar por nombre
                                if (datoTextBox != null)
                                {
                                    datoTextBox.IsVisible = true;
                                    datoTextBox.KeyDown -= DatoTextBox_KeyDown_BuscarEmpleadoNombre;
                                    datoTextBox.KeyDown += DatoTextBox_KeyDown_BuscarEmpleadoNombre;
                                }
                                break;
                        }
                        break;
                }
            }
        }

        // Handler for buscar por nombre o ID (Clientes)
        private void DatoTextBox_KeyDown_BuscarNombreId(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var datoTextBox = sender as TextBox;
                if (datoTextBox == null || string.IsNullOrWhiteSpace(datoTextBox.Text)) return;
                if (_dao == null) return;
                try
                {
                    string input = datoTextBox.Text.Trim();
                    string sql;
                    var parameters = new Dictionary<string, object>();
                    // Buscar tanto por ID_CLIENTE exacto como por nombre de cliente
                    // ID_CLIENTE es VARCHAR2(100), por lo que puede ser cualquier string
                    sql = "SELECT * FROM CLIENTE WHERE ID_CLIENTE = :id OR LOWER(NOMBRE_CLIENTE) LIKE :nombre";
                    parameters["id"] = input;
                    parameters["nombre"] = $"%{input.ToLower()}%";

                    var dt = _dao.ExecuteQuery(sql, parameters);
                    var dtReordenado = ReordenarColumnasCliente(dt);
                    _lastDataTable = dtReordenado;
                    _lastEntidad = "Clientes";
                    paginaActual = 1;
                    filasFiltradas = dtReordenado.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaPaginada(dtReordenado, "Clientes");
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        // Populate DistribuidorComboBox with unique distribuidores
        private void CargarDistribuidores(ComboBox combo)
        {
            if (_dao == null) return;
            var lista = new List<(string Id, string Nombre)>();
            string sql = "SELECT ID_DISTRIBUIDOR, NOMBRE_DISTRIBUIDOR FROM DISTRIBUIDOR ORDER BY NOMBRE_DISTRIBUIDOR";
            var dt = _dao.ExecuteQuery(sql, new Dictionary<string, object>());
            foreach (DataRow row in dt.Rows)
            {
                if (row["ID_DISTRIBUIDOR"] != null && row["NOMBRE_DISTRIBUIDOR"] != null)
                    lista.Add((row["ID_DISTRIBUIDOR"].ToString() ?? "", row["NOMBRE_DISTRIBUIDOR"].ToString() ?? ""));
            }

            // Añadir opción Nutralia
            lista.Add(("NUTRALIA", "Nutralia"));
            combo.ItemsSource = lista.Select(x => x.Nombre).ToList();
            combo.Tag = lista; // Guardar la lista de tuplas para el handler
            combo.SelectedIndex = lista.Count > 0 ? 0 : -1;
        }

        // Handler for buscar por distribuidor (Clientes)


        // Handler for buscar por distribuidor (Clientes) - OLD VERSION
        private void DistribuidorComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (_dao == null) return;
            if (combo?.SelectedItem is string nombreDistribuidor && !string.IsNullOrWhiteSpace(nombreDistribuidor))
            {
                try
                {
                    var lista = combo.Tag as List<(string Id, string Nombre)>;
                    var distribuidor = lista?.FirstOrDefault(x => x.Nombre == nombreDistribuidor);
                    if (distribuidor.HasValue)
                    {
                        string sql;
                        Dictionary<string, object> parameters;
                        if (distribuidor.Value.Id == "NUTRALIA")
                        {
                            sql = "SELECT * FROM CLIENTE WHERE DISTRIBUIDOR = 'NUTRALIA'";
                            parameters = new Dictionary<string, object>();
                        }
                        else
                        {
                            sql = "SELECT * FROM CLIENTE WHERE DISTRIBUIDOR = :distribuidor";
                            parameters = new Dictionary<string, object> { ["distribuidor"] = distribuidor.Value.Id };
                        }

                        var dt = _dao.ExecuteQuery(sql, parameters);
                        var dtReordenado = ReordenarColumnasCliente(dt);
                        _lastDataTable = dtReordenado;
                        _lastEntidad = "Clientes";
                        paginaActual = 1;
                        filasFiltradas = dtReordenado.Rows.Cast<DataRow>().ToList();
                        totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                        MostrarTablaPaginada(dtReordenado, "Clientes");
                    }
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        // Handler for buscar por categoria (Clientes)
        private void CategoriaComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (_dao == null) return;
            if (combo?.SelectedItem is string categoria && !string.IsNullOrWhiteSpace(categoria))
            {
                try
                {
                    // Deshabilitar auto-refresh temporalmente
                    _autoRefreshTimer?.Stop();
                    
                    string sql = "SELECT * FROM CLIENTE WHERE CATEGORIA = :categoria";
                    var parameters = new Dictionary<string, object> { ["categoria"] = categoria };
                    var dt = _dao.ExecuteQuery(sql, parameters);
                    var dtReordenado = ReordenarColumnasCliente(dt);
                    _lastDataTable = dtReordenado;
                    _lastEntidad = "Clientes";
                    paginaActual = 1;
                    filasFiltradas = dtReordenado.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaPaginada(dtReordenado, "Clientes");
                    
                    // Reactivar auto-refresh después de un delay
                    Task.Delay(2000).ContinueWith(_ => _autoRefreshTimer?.Start());
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        // Handler for buscar por ID (Pedido)
        private void DatoTextBox_KeyDown_BuscarPedidoId(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var datoTextBox = sender as TextBox;
                if (datoTextBox == null || string.IsNullOrWhiteSpace(datoTextBox.Text)) return;
                if (_dao == null) return;
                try
                {
                    string input = datoTextBox.Text.Trim();
                    string sql;
                    var parameters = new Dictionary<string, object>();
                    
                    // Buscar por ID_PEDIDO exacto o por ID_CLIENTE e incluir empresa desde tabla EMPRESA
                    sql = @"
                        SELECT p.ID_PEDIDO, p.FECHA_REGISTRO, p.FECHA_PREPARACION, p.FECHA_RECOGIDA, 
                               p.HORA_PREPARACION, p.HORA_RECOGIDA, p.FECHA_ENTREGA, p.ESTADO, 
                               p.COMENTARIOS, p.ID_CLIENTE, p.ID_EMPLEADO, p.ID_DISTRIBUIDOR,
                               p.ID_EMPRESA, e.NOMBRE_EMPRESA 
                        FROM PEDIDO p 
                        LEFT JOIN CLIENTE c ON p.ID_CLIENTE = c.ID_CLIENTE 
                        LEFT JOIN EMPRESA e 
                          ON TRIM(UPPER(p.ID_EMPRESA)) = TRIM(UPPER(e.ID_EMPRESA)) 
                        WHERE p.ID_PEDIDO = :id OR p.ID_CLIENTE = :id";
                    parameters["id"] = input;
                    var dt = _dao.ExecuteQuery(sql, parameters);
                    _lastDataTable = dt;
                    _lastEntidad = "Pedido";
                    _selectedEntity = "Pedido";
                    paginaActual = 1;
                    filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaPaginada(dt, "Pedido");
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        // Handler for buscar por cliente (Pedido)
        private void DatoTextBox_KeyDown_BuscarPedidoCliente(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var datoTextBox = sender as TextBox;
                if (datoTextBox == null || string.IsNullOrWhiteSpace(datoTextBox.Text)) return;
                if (_dao == null) return;
                try
                {
                    string input = datoTextBox.Text.Trim();
                    string sql;
                    var parameters = new Dictionary<string, object>();
                    // Buscar todos los pedidos de un cliente específico
                    // Puede ser por ID_CLIENTE exacto o por nombre de cliente
                    if (input.All(char.IsDigit) || input.Length <= 10) // Si parece un ID
                    {
                        // Buscar por ID_CLIENTE exacto e incluir empresa desde tabla EMPRESA
                        sql = @"
                            SELECT p.ID_PEDIDO, p.FECHA_REGISTRO, p.FECHA_PREPARACION, p.FECHA_RECOGIDA, 
                                   p.HORA_PREPARACION, p.HORA_RECOGIDA, p.FECHA_ENTREGA, p.ESTADO, 
                                   p.COMENTARIOS, p.ID_CLIENTE, p.ID_EMPLEADO, p.ID_DISTRIBUIDOR,
                                   p.ID_EMPRESA, e.NOMBRE_EMPRESA 
                            FROM PEDIDO p 
                            LEFT JOIN CLIENTE c ON p.ID_CLIENTE = c.ID_CLIENTE 
                            LEFT JOIN EMPRESA e 
                              ON TRIM(UPPER(p.ID_EMPRESA)) = TRIM(UPPER(e.ID_EMPRESA)) 
                            WHERE p.ID_CLIENTE = :id";
                        parameters["id"] = input;
                    }
                    else
                    {
                        // Buscar por nombre de cliente e incluir empresa desde tabla EMPRESA
                        sql = @"
                            SELECT p.ID_PEDIDO, p.FECHA_REGISTRO, p.FECHA_PREPARACION, p.FECHA_RECOGIDA, 
                                   p.HORA_PREPARACION, p.HORA_RECOGIDA, p.FECHA_ENTREGA, p.ESTADO, 
                                   p.COMENTARIOS, p.ID_CLIENTE, p.ID_EMPLEADO, p.ID_DISTRIBUIDOR,
                                   p.ID_EMPRESA, e.NOMBRE_EMPRESA 
                            FROM PEDIDO p 
                            INNER JOIN CLIENTE c ON p.ID_CLIENTE = c.ID_CLIENTE 
                            LEFT JOIN EMPRESA e 
                              ON TRIM(UPPER(p.ID_EMPRESA)) = TRIM(UPPER(e.ID_EMPRESA)) 
                            WHERE LOWER(c.NOMBRE_CLIENTE) LIKE :nombre";
                        parameters["nombre"] = $"%{input.ToLower()}%";
                    }

                    var dt = _dao.ExecuteQuery(sql, parameters);
                    _lastDataTable = dt;
                    _lastEntidad = "Pedido";
                    _selectedEntity = "Pedido";
                    paginaActual = 1;
                    filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaPaginada(dt, "Pedido");
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        // Pago: Mostrar todos
        private void MostrarTodosPagos()
        {
            if (_dao == null)
                return;
            try
            {
                var dt = _dao.ExecuteQuery("SELECT * FROM PAGO", new Dictionary<string, object>());
                _lastDataTable = dt;
                _lastEntidad = "Pago";
                _selectedEntity = "Pago";
                filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                paginaActual = 1;
                totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                MostrarTablaPaginada(dt, "Pago");
            }
            catch (Exception ex)
            {
                var panel = this.FindControl<StackPanel>("TablaResultado");
                if (panel != null)
                {
                    panel.Children.Clear();
                    panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                }
            }
        }

        // Handler for buscar pago por ID
        private void DatoTextBox_KeyDown_BuscarPagoId(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var datoTextBox = sender as TextBox;
                if (datoTextBox == null || string.IsNullOrWhiteSpace(datoTextBox.Text)) return;
                if (_dao == null) return;
                try
                {
                    string input = datoTextBox.Text.Trim();
                    string sql;
                    var parameters = new Dictionary<string, object>();
                    
                    // Buscar por ID_TRANSACCION exacto
                    // ID_TRANSACCION es VARCHAR2, por lo que puede ser cualquier string
                    sql = "SELECT * FROM PAGO WHERE ID_TRANSACCION = :id";
                    parameters["id"] = input;
                    var dt = _dao.ExecuteQuery(sql, parameters);
                    _lastDataTable = dt;
                    _lastEntidad = "Pago";
                    _selectedEntity = "Pago";
                    paginaActual = 1;
                    filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaPaginada(dt, "Pago");
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        // Handler for buscar pago por cliente
        private void DatoTextBox_KeyDown_BuscarPagoCliente(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var datoTextBox = sender as TextBox;
                if (datoTextBox == null || string.IsNullOrWhiteSpace(datoTextBox.Text)) return;
                if (_dao == null) return;
                try
                {
                    string input = datoTextBox.Text.Trim();
                    string sql;
                    var parameters = new Dictionary<string, object>();
                    
                    // Buscar por código de cliente (exacto) o por nombre de cliente (parcial)
                    // ID_CLIENTE es VARCHAR2, por lo que podemos buscar tanto por código como por nombre
                    
                    // Sanitizar input más agresivamente
                    input = input.Trim();
                    
                    // Buscar tanto por ID_CLIENTE exacto como por nombre de cliente
                    // ID_CLIENTE es VARCHAR2(100), por lo que puede ser cualquier string
                    sql = "SELECT * FROM PAGO WHERE ID_CLIENTE = :id OR ID_CLIENTE IN (SELECT ID_CLIENTE FROM CLIENTE WHERE LOWER(NOMBRE_CLIENTE) LIKE :nombre)";
                    parameters["id"] = input;
                    parameters["nombre"] = $"%{input.ToLower()}%";

                    var dt = _dao.ExecuteQuery(sql, parameters);
                    _lastDataTable = dt;
                    _lastEntidad = "Pago";
                    _selectedEntity = "Pago";
                    paginaActual = 1;
                    filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaPaginada(dt, "Pago");
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        // Empleados: Mostrar todos
        private void MostrarTodosEmpleados()
        {
            if (_dao == null)
                return;
            try
            {
                var dt = _dao.ExecuteQuery("SELECT * FROM EMPLEADO", new Dictionary<string, object>());
                _lastDataTable = dt;
                _lastEntidad = "Empleados";
                _selectedEntity = "Empleados";
                filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                paginaActual = 1;
                totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                MostrarTablaPaginada(dt, "Empleados");
            }
            catch (Exception ex)
            {
                var panel = this.FindControl<StackPanel>("TablaResultado");
                if (panel != null)
                {
                    panel.Children.Clear();
                    panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                }
            }
        }

        private void MostrarTodosPedidos()
        {
            if (_dao == null)
            {
                MostrarMensaje("Error: No hay conexión a la base de datos", true);
                return;
            }
            
            try
            {
                // Limpiar variables de estado
                _sortColumn = null;
                _sortDescending = true;
                
                // Consulta con JOIN para incluir información de empresa desde tabla EMPRESA
                var sql = @"
                    SELECT p.ID_PEDIDO, p.FECHA_REGISTRO, p.FECHA_PREPARACION, p.FECHA_RECOGIDA, 
                           p.HORA_PREPARACION, p.HORA_RECOGIDA, p.FECHA_ENTREGA, p.ESTADO, 
                           p.COMENTARIOS, p.ID_CLIENTE, p.ID_EMPLEADO, p.ID_DISTRIBUIDOR,
                           p.ID_EMPRESA, e.NOMBRE_EMPRESA 
                    FROM PEDIDO p 
                    LEFT JOIN EMPRESA e 
                      ON TRIM(UPPER(p.ID_EMPRESA)) = TRIM(UPPER(e.ID_EMPRESA)) 
                    ORDER BY p.ID_PEDIDO";
                
                var dt = _dao.ExecuteQuery(sql, new Dictionary<string, object>());
                
                _lastDataTable = dt;
                _lastEntidad = "Pedido";
                _selectedEntity = "Pedido";
                filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                paginaActual = 1;
                totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                MostrarTablaPaginada(dt, "Pedido");
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al cargar pedidos: {ex.Message}", true);
                var panel = this.FindControl<StackPanel>("TablaResultado");
                if (panel != null)
                {
                    panel.Children.Clear();
                    panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                }
            }
        }

        private void MostrarPedidosRumm()
        {
            if (_dao == null)
                return;
            try
            {
                // Limpiar variables de estado
                _sortColumn = null;
                _sortDescending = true;
                
                // Consulta para filtrar pedidos de Rumm (ID_EMPRESA = '2')
                var sql = @"
                    SELECT p.ID_PEDIDO, p.FECHA_REGISTRO, p.FECHA_PREPARACION, p.FECHA_RECOGIDA, 
                           p.HORA_PREPARACION, p.HORA_RECOGIDA, p.FECHA_ENTREGA, p.ESTADO, 
                           p.COMENTARIOS, p.ID_CLIENTE, p.ID_EMPLEADO, p.ID_DISTRIBUIDOR,
                           p.ID_EMPRESA, e.NOMBRE_EMPRESA 
                    FROM PEDIDO p 
                    LEFT JOIN EMPRESA e 
                      ON TRIM(UPPER(p.ID_EMPRESA)) = TRIM(UPPER(e.ID_EMPRESA)) 
                    WHERE p.ID_EMPRESA = '2'
                    ORDER BY p.ID_PEDIDO";
                
                var dt = _dao.ExecuteQuery(sql, new Dictionary<string, object>());
                _lastDataTable = dt;
                _lastEntidad = "Pedido";
                _selectedEntity = "Pedido";
                filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                paginaActual = 1;
                totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                MostrarTablaPaginada(dt, "Pedido");
            }
            catch (Exception ex)
            {
                var panel = this.FindControl<StackPanel>("TablaResultado");
                if (panel != null)
                {
                    panel.Children.Clear();
                    panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                }
            }
        }

        private void MostrarPedidosSQLmanager()
        {
            if (_dao == null)
                return;
            try
            {
                // Limpiar variables de estado
                _sortColumn = null;
                _sortDescending = true;
                
                // Consulta para filtrar pedidos de SQLmanager (ID_EMPRESA = '1')
                var sql = @"
                    SELECT p.ID_PEDIDO, p.FECHA_REGISTRO, p.FECHA_PREPARACION, p.FECHA_RECOGIDA, 
                           p.HORA_PREPARACION, p.HORA_RECOGIDA, p.FECHA_ENTREGA, p.ESTADO, 
                           p.COMENTARIOS, p.ID_CLIENTE, p.ID_EMPLEADO, p.ID_DISTRIBUIDOR,
                           p.ID_EMPRESA, e.NOMBRE_EMPRESA 
                    FROM PEDIDO p 
                    LEFT JOIN EMPRESA e 
                      ON TRIM(UPPER(p.ID_EMPRESA)) = TRIM(UPPER(e.ID_EMPRESA)) 
                    WHERE p.ID_EMPRESA = '1'
                    ORDER BY p.ID_PEDIDO";
                
                var dt = _dao.ExecuteQuery(sql, new Dictionary<string, object>());
                _lastDataTable = dt;
                _lastEntidad = "Pedido";
                _selectedEntity = "Pedido";
                filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                paginaActual = 1;
                totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                MostrarTablaPaginada(dt, "Pedido");
            }
            catch (Exception ex)
            {
                var panel = this.FindControl<StackPanel>("TablaResultado");
                if (panel != null)
                {
                    panel.Children.Clear();
                    panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                }
            }
        }

        // Método genérico para mostrar cualquier DataTable como tabla visual paginada
        private async void MostrarTablaPaginada(DataTable dt, string entidad)
        {
            var panel = this.FindControl<StackPanel>("TablaResultado");
            var paginaText = this.FindControl<TextBlock>("PaginaActualText");
            var btnAnt = this.FindControl<Button>("AnteriorPaginaButton");
            var btnSig = this.FindControl<Button>("SiguientePaginaButton");
               var paginaTextBox = this.FindControl<TextBox>("PaginaActualTextBox");
if (paginaTextBox != null)
    paginaTextBox.Text = paginaActual.ToString();
var totalPaginasText = this.FindControl<TextBlock>("TotalPaginasText");
if (totalPaginasText != null)
    totalPaginasText.Text = $"/ {totalPaginas}";
            TextBox? toFocus = null;
            if (panel == null) return;
            panel.Children.Clear();
            if (!_isAutoRefresh)
            {
                var loading = new TextBlock
                {
                    Text = "Cargando...", FontWeight = Avalonia.Media.FontWeight.Bold, FontSize = 18,
                    Foreground = Avalonia.Media.Brushes.DarkSlateGray, Margin = new Thickness(10)
                };
                panel.Children.Add(loading);
                await System.Threading.Tasks.Task.Delay(80);
                panel.Children.Clear();
            }

            if (dt.Rows.Count == 0)
            {
                panel.Children.Add(new TextBlock { Text = "Sin datos" });
                if (paginaText != null) paginaText.Text = "Página 0 de 0";
                if (btnAnt != null) btnAnt.IsEnabled = false;
                if (btnSig != null) btnSig.IsEnabled = false;
                return;
            }

            // Ordenar si corresponde
            DataTable dtSorted = dt;
            if (!string.IsNullOrEmpty(_sortColumn) && dt.Columns.Contains(_sortColumn))
            {
                var dv = new DataView(dt);
                dv.Sort = $"[{_sortColumn}] {(_sortDescending ? "ASC" : "DESC")}";
                dtSorted = dv.ToTable();
            }

            int inicio = (paginaActual - 1) * FilasPorPagina;
            int fin = Math.Min(inicio + FilasPorPagina, dtSorted.Rows.Count);
            var columns = dtSorted.Columns.Cast<DataColumn>().ToList();
            // Manejar columnas de empresa según la entidad
            if (entidad == "Pedido")
            {
                // Debug: Verificar columnas antes del reordenamiento
                Console.WriteLine($"DEBUG: Columnas antes del reordenamiento: {string.Join(", ", columns.Select(c => c.ColumnName))}");
                
                // Para Pedido, mantener ID_EMPRESA y NOMBRE_EMPRESA y ponerlos en primeras posiciones
                var idEmpresaCol = columns.FirstOrDefault(x => x.ColumnName == "ID_EMPRESA");
                var nombreEmpresaCol = columns.FirstOrDefault(x => x.ColumnName == "NOMBRE_EMPRESA");
                
                Console.WriteLine($"DEBUG: ID_EMPRESA encontrado: {idEmpresaCol != null}");
                Console.WriteLine($"DEBUG: NOMBRE_EMPRESA encontrado: {nombreEmpresaCol != null}");
                
                if (idEmpresaCol != null)
                {
                    columns.Remove(idEmpresaCol);
                    columns.Insert(0, idEmpresaCol); // Poner ID_EMPRESA en primera posición
                }
                
                if (nombreEmpresaCol != null)
                {
                    columns.Remove(nombreEmpresaCol);
                    columns.Insert(1, nombreEmpresaCol); // Poner NOMBRE_EMPRESA en segunda posición
                }
                
                // Debug: Verificar columnas después del reordenamiento
                Console.WriteLine($"DEBUG: Columnas después del reordenamiento: {string.Join(", ", columns.Select(c => c.ColumnName))}");
            }
            else if (entidad != "Clientes")
            {
                // Ocultar columnas de empresa en otras entidades excepto Clientes y Pedido
                columns = columns.Where(col => col.ColumnName != "NOMBRE_EMPRESA" && col.ColumnName != "ID_EMPRESA").ToList();
                if (dtSorted.Columns.Contains("NOMBRE_EMPRESA"))
                {
                    dtSorted.Columns.Remove("NOMBRE_EMPRESA");
                }
                if (dtSorted.Columns.Contains("ID_EMPRESA"))
                {
                    dtSorted.Columns.Remove("ID_EMPRESA");
                }
            }
            // Si es clientes, filtrar y reordenar columnas aquí también
            if (entidad == "Clientes")
            {
                var columnasPrioritarias = new[] { "VERIFICADO", "COMERCIAL", "CRONIDOL", "SOS", "RESPUESTA" };
                var columnasOcultas = new[]
                    { "VIABILIDAD", "FACT_H", "AFIN_H", "PUNTUAC_H", "FACT_V", "AFIN_V", "PUNTUAC_V" };
                var columnasReordenadas = new List<DataColumn>();
                foreach (var col in columnasPrioritarias)
                {
                    if (col == "CRONIDOL")
                    {
                        var c = columns.FirstOrDefault(x => x.ColumnName == "VIAB_HUMANO");
                        if (c != null) columnasReordenadas.Add(c);
                    }
                    else if (col == "SOS")
                    {
                        var c = columns.FirstOrDefault(x => x.ColumnName == "VIAB_VETERINARIO");
                        if (c != null) columnasReordenadas.Add(c);
                    }
                    else
                    {
                        var c = columns.FirstOrDefault(x => x.ColumnName == col);
                        if (c != null) columnasReordenadas.Add(c);
                        
                        // Insertar COMENTARIOS justo después de RESPUESTA
                        if (col == "RESPUESTA")
                        {
                            var comentariosCol = columns.FirstOrDefault(x => x.ColumnName == "COMENTARIOS");
                            if (comentariosCol != null && !columnasReordenadas.Contains(comentariosCol))
                            {
                                columnasReordenadas.Add(comentariosCol);
                                Console.WriteLine("[DEBUG] COMENTARIOS insertado después de RESPUESTA en MostrarTablaPaginada");
                            }
                        }
                    }
                }

                var resto = columns.Where(col =>
                    !columnasReordenadas.Contains(col) && !columnasOcultas.Contains(col.ColumnName) &&
                    col.ColumnName != "TELEFONO" && col.ColumnName != "EMAIL" && col.ColumnName != "NOMBRE_CONTACTO" &&
                    col.ColumnName != "APELLIDO_CONTACTO" && col.ColumnName != "NOMBRE_EMPRESA" &&
                    col.ColumnName != "NOMBRE_CLIENTE" && col.ColumnName != "DISTRIBUIDOR" && 
                    col.ColumnName != "COMENTARIOS").ToList();
                var nombreClienteCol = columns.FirstOrDefault(x => x.ColumnName == "NOMBRE_CLIENTE");
                var nombreEmpresaCol = columns.FirstOrDefault(x => x.ColumnName == "NOMBRE_EMPRESA");
                if (nombreClienteCol != null && !columnasReordenadas.Contains(nombreClienteCol))
                    columnasReordenadas.Add(nombreClienteCol);
                if (nombreEmpresaCol != null && !columnasReordenadas.Contains(nombreEmpresaCol))
                    columnasReordenadas.Add(nombreEmpresaCol);
                var distribuidorCol = columns.FirstOrDefault(x => x.ColumnName == "DISTRIBUIDOR");
                if (distribuidorCol != null) columnasReordenadas.Add(distribuidorCol);
                var telCol = columns.FirstOrDefault(x => x.ColumnName == "TELEFONO");
                if (telCol != null) columnasReordenadas.Add(telCol);
                var emailCol = columns.FirstOrDefault(x => x.ColumnName == "EMAIL");
                if (emailCol != null) columnasReordenadas.Add(emailCol);
                var nombreContactoCol = columns.FirstOrDefault(x => x.ColumnName == "NOMBRE_CONTACTO");
                if (nombreContactoCol != null) columnasReordenadas.Add(nombreContactoCol);
                var apellidoContactoCol = columns.FirstOrDefault(x => x.ColumnName == "APELLIDO_CONTACTO");
                if (apellidoContactoCol != null) columnasReordenadas.Add(apellidoContactoCol);
                columnasReordenadas.AddRange(resto);
                columns = columnasReordenadas;
                // Si NOMBRE_EMPRESA no existe, agrégalo como columna vacía (pero solo si no está ya)
                if (!dt.Columns.Contains("NOMBRE_EMPRESA") && !columns.Any(x => x.ColumnName == "NOMBRE_EMPRESA"))
                {
                    dt.Columns.Add("NOMBRE_EMPRESA");
                    foreach (DataRow row in dt.Rows)
                        row["NOMBRE_EMPRESA"] = string.Empty;
                }
            }

            int numCols = columns.Count;
            int numRows = 1 + (fin - inicio);
            var tableGrid = new Grid { Background = Avalonia.Media.Brushes.Transparent };
            for (int i = 0; i < numCols; i++)
                tableGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (int r = 0; r < numRows; r++)
                tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            // Cabecera
            for (int col = 0; col < numCols; col++)
            {
                Control cellControl = new TextBlock { Text = "" };
                var colName = columns[col].ColumnName;
                string header = colName;
                
                // Debug: Verificar que se procesan las columnas de empresa
                if (entidad == "Pedido" && (colName == "ID_EMPRESA" || colName == "NOMBRE_EMPRESA"))
                {
                    Console.WriteLine($"DEBUG: Procesando cabecera columna {col}: {colName}");
                }
                if (entidad == "Clientes")
                {
                    if (colName == "ID_CLIENTE") header = "CIF";
                    else if (colName == "VIAB_HUMANO") header = "Cronidol";
                    else if (colName == "VIAB_VETERINARIO") header = "SOS";
                    else if (colName == "NOMBRE_CLIENTE") header = "Razón Social";
                    else if (colName == "NOMBRE_EMPRESA") header = "Nombre Empresa";
                    else if (colName == "DISTRIBUIDOR") header = "Distribuidor";
                    else header = colName.Replace("_", " ");
                }
                else if (entidad == "Clientes Asociados")
                {
                    if (colName == "ID_CLIENTE_ASOCIADO") header = "ID Cliente";
                    else if (colName == "ID_DISTRIBUIDOR") header = "Nombre Distribuidor";
                    else if (colName == "NOMBRE_CLIENTE_ASOCIADO") header = "Nombre Cliente";
                    else if (colName == "NOMBRE_CONTACTO_ASOCIADO") header = "Nombre Contacto";
                    else if (colName == "APELLIDO_CONTACTO_ASOCIADO") header = "Apellido Contacto";
                    else if (colName == "TELEFONO_ASOCIADO") header = "Teléfono";
                    else if (colName == "LINEA_DIRECCION1_ASOCIADA") header = "Dirección 1";
                    else if (colName == "LINEA_DIRECCION2_ASOCIADA") header = "Dirección 2";
                    else if (colName == "CIUDAD_ASOCIADA") header = "Ciudad";
                    else if (colName == "REGION_ASOCIADA") header = "Región";
                    else if (colName == "PAIS_ASOCIADO") header = "País";
                    else if (colName == "CODIGO_POSTAL_ASOCIADO") header = "Código Postal";
                    else header = colName.Replace("_", " ");
                }

                var th = new TextBlock
                {
                    Text = header + (_sortColumn == colName ? (_sortDescending ? " ↓" : " ↑") : ""),
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    Margin = new Thickness(8, 0),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                };
                th.PointerPressed += (s, e) =>
                {
                    if (_sortColumn == colName)
                        _sortDescending = !_sortDescending;
                    else
                    {
                        _sortColumn = colName;
                        _sortDescending = true;
                    }

                    MostrarTablaPaginada(_lastDataTable ?? dt, entidad);
                };
                Grid.SetRow(th, 0);
                Grid.SetColumn(th, col);
                tableGrid.Children.Add(th);
            }

            // Filas de datos
            for (int i = inicio; i < fin; i++)
            {
                var row = dtSorted.Rows[i];
                int gridRow = 1 + (i - inicio);
                var zebra = (i % 2 == 0)
                    ? Avalonia.Media.Brushes.White
                    : new Avalonia.Media.BrushConverter().ConvertFromString("#F0F6FF") as Avalonia.Media.IBrush;
                var isSelected = (_selectedClienteRow.HasValue && _selectedClienteRow.Value == i);
                for (int col = 0; col < numCols; col++)
                {
                    var colName = columns[col].ColumnName;
                    Control cellControl = new TextBlock { Text = "" };
                    if (entidad == "Clientes" && colName == "EMAIL")
                    {
                        var email = row[colName]?.ToString() ?? "";

                        var emailTextBlock = new TextBlock
                        {
                            Text = string.IsNullOrEmpty(email) ? "(-)" : email,
                            Foreground = string.IsNullOrEmpty(email) ? Avalonia.Media.Brushes.Gray : Avalonia.Media.Brushes.Blue,
                            TextDecorations = Avalonia.Media.TextDecorationCollection.Parse("Underline"),
                            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            IsVisible = true
                        };

                        var emailTextBox = new TextBox
                        {
                            Text = string.IsNullOrEmpty(email) ? "(-)" : email,
                            Foreground = Avalonia.Media.Brushes.Black,
                            Background = isSelected ? Avalonia.Media.Brushes.LightSkyBlue : zebra,
                            BorderThickness = new Avalonia.Thickness(0),
                            Padding = new Avalonia.Thickness(4),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            IsVisible = false
                        };

                        var container = new Border
                        {
                            BorderBrush = Avalonia.Media.Brushes.Gray,
                            BorderThickness = new Avalonia.Thickness(1),
                            Background = isSelected ? Avalonia.Media.Brushes.LightSkyBlue : zebra,
                            Padding = new Avalonia.Thickness(0),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            Child = new Grid
                            {
                                Children =
                                {
                                    emailTextBlock,
                                    emailTextBox
                                }
                            }
                        };

                        emailTextBlock.PointerPressed += (s, e) =>
                        {
                            bool isDoubleClick = false;
                            System.Threading.Tasks.Task.Delay(250).ContinueWith(_ =>
                            {
                                if (!isDoubleClick)
                                {
                                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                    {
                                        if (emailTextBlock.IsVisible && !string.IsNullOrEmpty(emailTextBlock.Text) && emailTextBlock.Text != "(haz doble click para añadir)")
                                        {
                                            try
                                            {
                                                var mailtoUri = new Uri($"mailto:{emailTextBlock.Text}");
                                                var psi = new System.Diagnostics.ProcessStartInfo
                                                {
                                                    FileName = mailtoUri.ToString(),
                                                    UseShellExecute = true
                                                };
                                                System.Diagnostics.Process.Start(psi);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Error al abrir email: {ex.Message}");
                                            }
                                        }
                                    });
                                }
                            });
                        };

                        emailTextBlock.DoubleTapped += (s, e) =>
                        {
                            emailTextBlock.IsVisible = false;
                            emailTextBox.IsVisible = true;
                            emailTextBox.Focus();
                            emailTextBox.SelectAll();
                        };

                        emailTextBox.LostFocus += (s, e) =>
                        {
                            var nuevoEmail = emailTextBox.Text ?? "";
                            if (nuevoEmail == "(-)")
                                nuevoEmail = "";

                            row[colName] = nuevoEmail;
                            // Guardar en la base de datos, igual que el resto de columnas editables
                            GuardarCelda(row, colName, nuevoEmail);

                            emailTextBlock.Text = string.IsNullOrEmpty(nuevoEmail) ? "(-)" : nuevoEmail;
                            emailTextBlock.Foreground = string.IsNullOrEmpty(nuevoEmail) ? Avalonia.Media.Brushes.Gray : Avalonia.Media.Brushes.Blue;
                            emailTextBlock.IsVisible = true;
                            emailTextBox.IsVisible = false;

                            tableGrid.InvalidateVisual();
                        };

                        emailTextBox.KeyDown += (s, e) =>
                        {
                            try
                            {
                                if (e.Key == Avalonia.Input.Key.Enter)
                                {
                                    var nuevoEmail = emailTextBox.Text ?? "";
                                    if (nuevoEmail == "(-)")
                                        nuevoEmail = "";

                                    row[colName] = nuevoEmail;
                                    // Guardar en la base de datos al pulsar Enter
                                    GuardarCelda(row, colName, nuevoEmail);

                                    emailTextBlock.Text = string.IsNullOrEmpty(nuevoEmail) ? "(-)" : nuevoEmail;
                                    emailTextBlock.Foreground = string.IsNullOrEmpty(nuevoEmail) ? Avalonia.Media.Brushes.Gray : Avalonia.Media.Brushes.Blue;
                                    emailTextBlock.IsVisible = true;
                                    emailTextBox.IsVisible = false;

                                    tableGrid.InvalidateVisual();
                                    e.Handled = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                MostrarMensaje($"Error al guardar celda: {ex.Message}", true);
                            }
                        };

                        cellControl = container;

                    }
                    else if (entidad == "Clientes" && colName == "VERIFICADO")
                    {
                        var check = new CheckBox
                        {
                            IsChecked = row["VERIFICADO"].ToString() == "1",
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            IsEnabled = true
                        };
                        check.IsCheckedChanged += (s, e) =>
                            GuardarCelda(row, "VERIFICADO", check.IsChecked == true ? "1" : "0");
                        check.PointerReleased += (s, e) =>
                        {
                            _selectedClienteRow = i;
                            if (entidad == "Clientes" && dtSorted.Columns.Contains("ID_CLIENTE"))
                                _selectedClienteId = dtSorted.Rows[i]["ID_CLIENTE"]?.ToString();
                            tableGrid.InvalidateVisual();
                        };
                        var grid = new Grid
                        {
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        };
                        grid.Children.Add(check);
                        grid.PointerReleased += (s, e) =>
                        {
                            _selectedClienteRow = i;
                            if (entidad == "Clientes" && dtSorted.Columns.Contains("ID_CLIENTE"))
                                _selectedClienteId = dtSorted.Rows[i]["ID_CLIENTE"]?.ToString();
                            tableGrid.InvalidateVisual();
                        };
                        cellControl = grid;
                    }
                    else if (entidad == "Clientes Asociados" && colName == "ID_DISTRIBUIDOR")
                    {
                        var id = row[colName]?.ToString() ?? "";
                        var nombreDistribuidor = GetDistribuidores().FirstOrDefault(d => d.Id == id).Nombre ?? id;
                        var tb = new TextBox
                        {
                            Text = string.IsNullOrEmpty(nombreDistribuidor) ? id : nombreDistribuidor,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Background = isSelected ? Avalonia.Media.Brushes.LightSkyBlue : zebra,
                            Padding = new Thickness(8, 4),
                            FontSize = 14
                        };
                        tb.LostFocus += (s, e) =>
                        {
                            var nuevoValor = tb.Text ?? "";
                            row[colName] = nuevoValor;
                            // Guardar en la base de datos
                            GuardarCelda(row, colName, nuevoValor);
                        };
                        tb.KeyDown += (s, e) =>
                        {
                            if (e.Key == Avalonia.Input.Key.Enter)
                            {
                                var nuevoValor = tb.Text ?? "";
                                row[colName] = nuevoValor;
                                // Guardar en la base de datos
                                GuardarCelda(row, colName, nuevoValor);
                                tb.IsVisible = false;
                            }
                        };
                        cellControl = tb;
                    }
                    else if (entidad == "Clientes" && colName == "RESPUESTA")
                    {
                        var opciones = new[] { "Cambiar a vacío", "🟢 Aceptado", "🟡 Pendiente", "🔴 Rechazado" };
                        var valorActual = row[colName]?.ToString() ?? "";
                        
                        var comboBox = new ComboBox
                        {
                            ItemsSource = opciones,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            Padding = new Thickness(8, 4),
                            FontSize = 14
                        };
                        
                        if (string.IsNullOrEmpty(valorActual))
                        {
                            comboBox.SelectedItem = null;
                        }
                        else if (valorActual.Contains("Aceptado"))
                        {
                            comboBox.SelectedItem = "🟢 Aceptado";
                        }
                        else if (valorActual.Contains("Pendiente"))
                        {
                            comboBox.SelectedItem = "🟡 Pendiente";
                        }
                        else if (valorActual.Contains("Rechazado"))
                        {
                            comboBox.SelectedItem = "🔴 Rechazado";
                        }
                        else
                        {
                            comboBox.SelectedItem = valorActual;
                        }
                        
                        comboBox.SelectionChanged += (s, e) =>
                        {
                            var selected = comboBox.SelectedItem?.ToString();
                            if (selected == "Cambiar a vacío")
                            {
                                row[colName] = "";
                                comboBox.SelectedItem = null;
                                // Guardar en la base de datos
                                GuardarCelda(row, colName, "");
                            }
                            else if (!string.IsNullOrEmpty(selected))
                            {
                                row[colName] = selected;
                                // Guardar en la base de datos
                                GuardarCelda(row, colName, selected);
                            }
                        };
                        
                        cellControl = comboBox;
                    }
                    else if (entidad == "Clientes" && colName == "COMERCIAL")
                    {
                        var empleados = GetEmpleados();
                        var opciones = empleados.Select(e => $"{e.Id} - {e.Nombre}").ToList();
                        opciones.Insert(0, "Cambiar a vacío");
                        var valorActual = row[colName]?.ToString() ?? "";
                        var comboBox = new ComboBox
                        {
                            ItemsSource = opciones,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            Padding = new Thickness(8, 4),
                            FontSize = 14
                        };
                        if (string.IsNullOrEmpty(valorActual))
                            comboBox.SelectedItem = null;
                        else
                        {
                            var match = opciones.FirstOrDefault(o => o.StartsWith(valorActual));
                            comboBox.SelectedItem = match ?? valorActual;
                        }
                        comboBox.SelectionChanged += (s, e) =>
                        {
                            var selected = comboBox.SelectedItem?.ToString();
                            if (selected == "Cambiar a vacío")
                            {
                                row[colName] = "";
                                comboBox.SelectedItem = null;
                                // Guardar en la base de datos
                                GuardarCelda(row, colName, "");
                            }
                            else if (!string.IsNullOrEmpty(selected))
                            {
                                var id = selected.Split(' ')[0];
                                row[colName] = id;
                                // Guardar en la base de datos
                                GuardarCelda(row, colName, id);
                            }
                        };
                        
                        cellControl = comboBox;
                    }
                    else
                    {
                        var tb = new TextBox
                        {
                            Text = row[colName]?.ToString() ?? "",
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Background = isSelected ? Avalonia.Media.Brushes.LightSkyBlue : zebra
                        };
                        tb.LostFocus += (s, e) => {
                            try
                            {
                                var nuevoValor = tb.Text ?? "";
                                row[colName] = nuevoValor;
                                // Guardar en la base de datos
                                GuardarCelda(row, colName, nuevoValor);
                            }
                            catch (Exception ex)
                            {
                                MostrarMensaje($"Error al guardar celda: {ex.Message}", true);
                            }
                        };
                        tb.KeyDown += (s, e) => {
                            try
                            {
                                if (e.Key == Avalonia.Input.Key.Enter)
                                {
                                    var nuevoValor = tb.Text ?? "";
                                    row[colName] = nuevoValor;
                                    // Guardar en la base de datos
                                    GuardarCelda(row, colName, nuevoValor);
                                    e.Handled = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                MostrarMensaje($"Error al guardar celda: {ex.Message}", true);
                            }
                        };
                        tb.PointerReleased += (s, e) =>
                        {
                            try
                            {
                                if (entidad == "Clientes" && dtSorted.Columns.Contains("ID_CLIENTE") && i >= 0 && i < dtSorted.Rows.Count)
                                    _selectedClienteId = dtSorted.Rows[i]["ID_CLIENTE"]?.ToString();
                                _selectedClienteRow = i;
                                tableGrid.InvalidateVisual();
                            }
                            catch (Exception ex)
                            {
                                MostrarMensaje($"Error al seleccionar fila: {ex.Message}", true);
                            }
                        };
                        cellControl = tb;
                    }

                    Grid.SetRow(cellControl, gridRow);
                    Grid.SetColumn(cellControl, col);
                    tableGrid.Children.Add(cellControl);
                }
            }

            panel.Children.Add(tableGrid);
            // Restaurar foco y texto si corresponde
            if (toFocus != null && _editingText != null)
            {
                toFocus.Focus();
                toFocus.Text = _editingText;
                toFocus.CaretIndex = Math.Min(_editingCaret, toFocus.Text?.Length ?? 0);
            }

            _editingClienteId = null;
            _editingCol = null;
            _editingText = null;
            _editingCaret = 0;
            if (paginaText != null)
                paginaText.Text = $"Página {paginaActual} de {totalPaginas}";
            if (btnAnt != null)
                btnAnt.IsEnabled = paginaActual > 1;
            if (btnSig != null)
                btnSig.IsEnabled = paginaActual < totalPaginas;
            if (btnAnt != null) btnAnt.Click -= BtnAnt_Click;
            if (btnSig != null) btnSig.Click -= BtnSig_Click;
            if (btnAnt != null) btnAnt.Click += BtnAnt_Click;
            if (btnSig != null) btnSig.Click += BtnSig_Click;
            // Restaurar selección por ID tras refresco
            if (entidad == "Clientes" && _selectedClienteId != null && dt.Columns.Contains("ID_CLIENTE"))
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (dt.Rows[i]["ID_CLIENTE"]?.ToString() == _selectedClienteId)
                    {
                        _selectedClienteRow = i;
                        break;
                    }
                }

                _selectedClienteId = null;
            }

            // Al final del método, guarda la tabla renderizada para comparar en el próximo refresco
            if (entidad == "Clientes")
            {
                _lastRenderedTable = dt.Copy();
            }
        }

        private void GuardarCelda(DataRow row, string campo, string valor)
        {
            if (_dao == null) return;
            
            // No permitir edición de campos de fecha específicos
            if (campo == "FECHA_PAGO" || campo.Contains("FECHA") || campo.Contains("HORA") || campo.Contains("TIMESTAMP"))
            {
                return; // Salir sin hacer nada para campos de fecha
            }
            
            try
            {
                // Verificar que la fila y el campo existen
                if (row == null || !row.Table.Columns.Contains(campo))
                {
                    MostrarMensaje("Error: Campo no válido", true);
                    return;
                }
                
                // Determinar la tabla y el ID según la entidad actual
                string tabla = "";
                string idColumn = "";
                string id = "";
                
                switch (_selectedEntity)
                {
                    case "Clientes":
                        tabla = "CLIENTE";
                        idColumn = "ID_CLIENTE";
                        break;
                    case "Pedido":
                        tabla = "PEDIDO";
                        idColumn = "ID_PEDIDO";
                        break;
                    case "Pago":
                        tabla = "PAGO";
                        idColumn = "ID_TRANSACCION";
                        break;
                    case "Empleados":
                        tabla = "EMPLEADO";
                        idColumn = "ID_EMPLEADO";
                        break;
                    case "Clientes Asociados":
                        tabla = "CLIENTE_ASOCIADO";
                        idColumn = "ID_CLIENTE_ASOCIADO";
                        break;
                    default:
                        MostrarMensaje("Error: Entidad no soportada para edición", true);
                        return;
                }
                
                // Verificar que existe la columna ID
                if (!row.Table.Columns.Contains(idColumn))
                {
                    MostrarMensaje($"Error: Columna ID no encontrada para {_selectedEntity}", true);
                    return;
                }
                
                id = row[idColumn]?.ToString() ?? "";
                if (string.IsNullOrEmpty(id))
                {
                    MostrarMensaje($"Error: ID no válido para {_selectedEntity}", true);
                    return;
                }

                EnsureSafeUpdateTarget(tabla, campo, idColumn);
                
                // Manejar fechas TIMESTAMP WITH TIME ZONE
                string sql;
                var parametros = new Dictionary<string, object>();
                
                // Para campos de fecha, usar TO_TIMESTAMP_TZ para convertir string a TIMESTAMP WITH TIME ZONE
                if (campo.Contains("FECHA") || campo.Contains("HORA"))
                {
                    if (!string.IsNullOrWhiteSpace(valor))
                    {
                        sql = $"UPDATE {tabla} SET {campo} = TO_TIMESTAMP_TZ(:valor, 'DD/MM/YYYY HH24:MI:SS TZH:TZM') WHERE {idColumn} = :id";
                        parametros["valor"] = valor;
                    }
                    else
                    {
                        sql = $"UPDATE {tabla} SET {campo} = NULL WHERE {idColumn} = :id";
                    }
                }
                else
                {
                    // Para campos normales
                    sql = $"UPDATE {tabla} SET {campo} = :valor WHERE {idColumn} = :id";
                    parametros["valor"] = string.IsNullOrWhiteSpace(valor) ? DBNull.Value : (object)valor;
                }
                parametros["id"] = id;
                
                _dao.ExecuteNonQuery(sql, parametros);
                
                // Actualizar el DataTable como string
                row[campo] = string.IsNullOrWhiteSpace(valor) ? DBNull.Value : (object)valor;
            }
            catch (IndexOutOfRangeException ex)
            {
                MostrarMensaje($"Error de índice fuera de rango: {ex.Message}", true);
            }
            catch (Exception ex)
            {
                // Silenciar errores para campos de fecha
                if (!(campo.Contains("FECHA") || campo.Contains("HORA") || campo.Contains("TIMESTAMP")))
                {
                    MostrarMensaje($"Error al guardar: {ex.Message}", true);
                }
            }
        }

        private void EnsureSafeUpdateTarget(string tabla, string campo, string idColumn)
        {
            if (!_tablasEditables.Contains(tabla))
            {
                throw new InvalidOperationException($"Tabla no permitida para edicion: {tabla}");
            }

            if (!IsSafeSqlIdentifier(campo) || !IsSafeSqlIdentifier(idColumn))
            {
                throw new InvalidOperationException("Identificador SQL invalido para operacion de actualizacion");
            }
        }

        private static bool IsSafeSqlIdentifier(string identifier)
        {
            return !string.IsNullOrWhiteSpace(identifier) && _safeSqlIdentifier.IsMatch(identifier);
        }

        private void BtnAnt_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (paginaActual > 1)
            {
                paginaActual--;
                ActualizarTablaPaginada();
            }
        }

        private void BtnSig_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (paginaActual < totalPaginas)
            {
                paginaActual++;
                ActualizarTablaPaginada();
            }
        }

        private void DataGrid_CellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
        {
            if (e.Row.DataContext is Cliente cliente)
            {
                // Actualiza la base de datos con los nuevos valores del cliente editado
                if (_dao != null)
                {
                    var updateSql =
                        "UPDATE CLIENTE SET NOMBRE_CLIENTE = :NOMBRE_CLIENTE, NOMBRE_CONTACTO = :NOMBRE_CONTACTO, APELLIDO_CONTACTO = :APELLIDO_CONTACTO, LINEA_DIRECCION1 = :LINEA_DIRECCION1, LINEA_DIRECCION2 = :LINEA_DIRECCION2, CIUDAD = :CIUDAD, REGION = :REGION, PAIS = :PAIS, CODIGO_POSTAL = :CODIGO_POSTAL, VIABILIDAD = :VIABILIDAD, VIAB_HUMANO = :VIAB_HUMANO, VIAB_VETERINARIO = :VIAB_VETERINARIO, FACT_H = :FACT_H, AFIN_H = :AFIN_H, PUNTUAC_H = :PUNTUAC_H, FACT_V = :FACT_V, AFIN_V = :AFIN_V, PUNTUAC_V = :PUNTUAC_V, PUESTO = :PUESTO, CAMPO = :CAMPO, EMAIL = :EMAIL, VERIFICADO = :VERIFICADO, COMERCIAL = :COMERCIAL, RESPUESTA = :RESPUESTA, CATEGORIA = :CATEGORIA, COMENTARIOS = :COMENTARIOS, TELEFONO = :TELEFONO WHERE ID_CLIENTE = :ID_CLIENTE";
                    var parameters = new Dictionary<string, object>
                    {
                        ["ID_CLIENTE"] = cliente.ID_CLIENTE ?? string.Empty,
                        ["NOMBRE_CLIENTE"] = cliente.NOMBRE_CLIENTE ?? string.Empty,
                        ["NOMBRE_CONTACTO"] = cliente.NOMBRE_CONTACTO ?? string.Empty,
                        ["APELLIDO_CONTACTO"] = cliente.APELLIDO_CONTACTO ?? string.Empty,
                        ["LINEA_DIRECCION1"] = cliente.LINEA_DIRECCION1 ?? string.Empty,
                        ["LINEA_DIRECCION2"] = cliente.LINEA_DIRECCION2 ?? string.Empty,
                        ["CIUDAD"] = cliente.CIUDAD ?? string.Empty,
                        ["REGION"] = cliente.REGION ?? string.Empty,
                        ["PAIS"] = cliente.PAIS ?? string.Empty,
                        ["CODIGO_POSTAL"] = cliente.CODIGO_POSTAL ?? string.Empty,
                        ["VIABILIDAD"] = cliente.VIABILIDAD ?? string.Empty,
                        ["VIAB_HUMANO"] = cliente.VIAB_HUMANO ?? string.Empty,
                        ["VIAB_VETERINARIO"] = cliente.VIAB_VETERINARIO ?? string.Empty,
                        ["FACT_H"] = cliente.FACT_H ?? string.Empty,
                        ["AFIN_H"] = cliente.AFIN_H ?? string.Empty,
                        ["PUNTUAC_H"] = cliente.PUNTUAC_H ?? string.Empty,
                        ["FACT_V"] = cliente.FACT_V ?? string.Empty,
                        ["AFIN_V"] = cliente.AFIN_V ?? string.Empty,
                        ["PUNTUAC_V"] = cliente.PUNTUAC_V ?? string.Empty,
                        ["PUESTO"] = cliente.PUESTO ?? string.Empty,
                        ["CAMPO"] = cliente.CAMPO ?? string.Empty,
                        ["EMAIL"] = cliente.EMAIL ?? string.Empty,
                        ["VERIFICADO"] = cliente.VERIFICADO ?? string.Empty,
                        ["COMERCIAL"] = cliente.COMERCIAL ?? string.Empty,
                        ["RESPUESTA"] = cliente.RESPUESTA ?? string.Empty,
                        ["CATEGORIA"] = cliente.CATEGORIA ?? string.Empty,
                        ["COMENTARIOS"] = cliente.COMENTARIOS ?? string.Empty,
                        ["TELEFONO"] = cliente.TELEFONO ?? string.Empty,
                    };
                    _dao.ExecuteNonQuery(updateSql, parameters);
                }
            }
        }

        private List<(string Id, string Nombre)> GetDistribuidores()
        {
            if (_distribuidoresCache != null) return _distribuidoresCache;
            RefreshDistribuidoresCache();
            return _distribuidoresCache ?? new List<(string, string)>();
        }

        private void RefreshDistribuidoresCache()
        {
            _distribuidoresCache = new List<(string, string)>();
            if (_dao == null) return;
            try
            {
                var dt = _dao.ExecuteQuery("SELECT ID_DISTRIBUIDOR, NOMBRE_DISTRIBUIDOR FROM DISTRIBUIDOR ORDER BY NOMBRE_DISTRIBUIDOR", new Dictionary<string, object>());
                foreach (DataRow row in dt.Rows)
                {
                    var id = row["ID_DISTRIBUIDOR"]?.ToString() ?? "";
                    var nombre = row["NOMBRE_DISTRIBUIDOR"]?.ToString() ?? "";
                    _distribuidoresCache.Add((id, nombre));
                }
                // Opción extra conocida
                if (!_distribuidoresCache.Any(d => d.Id == "NUTRALIA"))
                    _distribuidoresCache.Add(("NUTRALIA", "Nutralia"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error al cargar distribuidores: {ex.Message}");
                _distribuidoresCache = new List<(string, string)>();
            }
        }


        private string GetDisplayName(string columnName, string entidad = "Clientes")
        {
            if (entidad == "Clientes")
            {
                return columnName switch
                {
                    "ID_CLIENTE" => "CIF",
                    "VIAB_HUMANO" => "Cronidol",
                    "VIAB_VETERINARIO" => "SOS",
                    "NOMBRE_CLIENTE" => "Razón Social",
                    "NOMBRE_EMPRESA" => "Nombre Empresa",
                    "DISTRIBUIDOR" => "Distribuidor",
                    _ => columnName.Replace("_", " ")
                };
            }
            else if (entidad == "Clientes Asociados")
            {
                return columnName switch
                {
                    "ID_CLIENTE_ASOCIADO" => "ID Cliente",
                    "ID_DISTRIBUIDOR" => "Nombre Distribuidor",
                    "NOMBRE_CLIENTE_ASOCIADO" => "Nombre Cliente",
                    "NOMBRE_CONTACTO_ASOCIADO" => "Nombre Contacto",
                    "APELLIDO_CONTACTO_ASOCIADO" => "Apellido Contacto",
                    "TELEFONO_ASOCIADO" => "Teléfono",
                    "LINEA_DIRECCION1_ASOCIADA" => "Dirección 1",
                    "LINEA_DIRECCION2_ASOCIADA" => "Dirección 2",
                    "CIUDAD_ASOCIADA" => "Ciudad",
                    "REGION_ASOCIADA" => "Región",
                    "PAIS_ASOCIADO" => "País",
                    "CODIGO_POSTAL_ASOCIADO" => "Código Postal",
                    _ => columnName.Replace("_", " ")
                };
            }
            else if (entidad == "Pedido")
            {
                return columnName switch
                {
                    "ID_EMPRESA" => "ID Empresa",
                    "NOMBRE_EMPRESA" => "Empresa",
                    "ID_PEDIDO" => "ID Pedido",
                    "FECHA_REGISTRO" => "Fecha Registro",
                    "FECHA_PREPARACION" => "Fecha Preparación",
                    "FECHA_RECOGIDA" => "Fecha Recogida",
                    "FECHA_ENTREGA" => "Fecha Entrega",
                    "HORA_PREPARACION" => "Hora Preparación",
                    "HORA_RECOGIDA" => "Hora Recogida",
                    "ESTADO" => "Estado",
                    "COMENTARIOS" => "Comentarios",
                    "ID_CLIENTE" => "ID Cliente",
                    "ID_EMPLEADO" => "ID Empleado",
                    "ID_DISTRIBUIDOR" => "Nombre Distribuidor",
                    _ => columnName.Replace("_", " ")
                };
            }
            else if (entidad == "Pago")
            {
                return columnName switch
                {
                    "ID_CLIENTE" => "ID Cliente",
                    "ID_TRANSACCION" => "ID Transacción",
                    "FORMA_PAGO" => "Forma de Pago",
                    "FECHA_PAGO" => "Fecha de Pago",
                    "TOTAL" => "Total",
                    _ => columnName.Replace("_", " ")
                };
            }
            else
            {
                return columnName.Replace("_", " ");
            }
        }

        // Método simplificado para formatear fechas para mostrar


        private void CreateDropdownForStatus(Border cellBorder, DataTable dtReordenado, int row, int col, int capturedRow, int capturedCol, 
            Avalonia.Media.IBrush successBorder, Avalonia.Media.IBrush errorBorder, Avalonia.Media.IBrush rowBackground)
        {
            // Opciones con iconos y colores
            var opciones = new[] { "Cambiar a vacío", "🟢 Aceptado", "🟡 Pendiente", "🔴 Rechazado" };
            
            // Obtener el valor actual de la celda
            var valorActual = dtReordenado.Rows[row][col]?.ToString() ?? "";
            
            var comboBox = new ComboBox
            {
                ItemsSource = opciones,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Padding = new Thickness(8, 4),
                FontSize = 14
            };
            
            // Establecer el valor seleccionado basado en el valor actual
            if (string.IsNullOrEmpty(valorActual))
            {
                comboBox.SelectedItem = null;
            }
            else if (valorActual.Contains("Aceptado"))
            {
                comboBox.SelectedItem = "🟢 Aceptado";
            }
            else if (valorActual.Contains("Pendiente"))
            {
                comboBox.SelectedItem = "🟡 Pendiente";
            }
            else if (valorActual.Contains("Rechazado"))
            {
                comboBox.SelectedItem = "🔴 Rechazado";
            }
            else
            {
                // Si el valor no coincide con ninguna opción, mostrar el valor actual como texto
                comboBox.SelectedItem = valorActual;
            }
            
            comboBox.SelectionChanged += async (s, e) =>
            {
                var selected = comboBox.SelectedItem?.ToString();
                if (selected == "Cambiar a vacío")
                {
                    dtReordenado.Rows[capturedRow][capturedCol] = "";
                    await TryUpdateCell(dtReordenado, capturedRow, capturedCol, new TextBox { Text = "" },
                        cellBorder, successBorder, errorBorder, rowBackground);
                    comboBox.SelectedItem = null; // Dejar el ComboBox vacío visualmente
                }
                else if (!string.IsNullOrEmpty(selected) && selected != valorActual)
                {
                    dtReordenado.Rows[capturedRow][capturedCol] = selected;
                    await TryUpdateCell(dtReordenado, capturedRow, capturedCol,
                        new TextBox { Text = selected }, cellBorder, successBorder, errorBorder,
                        rowBackground);
                }
            };
            
            cellBorder.Child = comboBox;
        }

        private void MostrarTablaComoVisual(DataTable dt, string entidad = "Clientes")
        {
            _lastDataTable = dt; // Store for export
            var panel = this.FindControl<StackPanel>("TablaResultado");
            if (panel == null)
                return;
            panel.Children.Clear();
            
            // Aplicar lógica específica según la entidad
            DataTable dtReordenado;
            if (entidad == "Clientes")
            {
                // Usar la lógica específica de Clientes
                dtReordenado = ReordenarColumnasCliente(dt);
            }
            else
            {
                // Para otras entidades, usar lógica genérica
                var columnasPrioritarias = new[] { "VERIFICADO", "COMERCIAL", "CRONIDOL", "SOS", "RESPUESTA" };
                var columnasOcultas = new[]
                    { "VIABILIDAD", "FACT_H", "AFIN_H", "PUNTUAC_H", "FACT_V", "AFIN_V", "PUNTUAC_V", "CATEGORIA" };
                var nuevasColumnas = columnasPrioritarias
                    .Where(c => dt.Columns.Contains(c == "CRONIDOL" ? "VIAB_HUMANO" : c == "SOS" ? "VIAB_VETERINARIO" : c))
                    .Select(c => c == "CRONIDOL" ? "VIAB_HUMANO" : c == "SOS" ? "VIAB_VETERINARIO" : c).ToList();
                nuevasColumnas.AddRange(
                    dt.Columns.Cast<DataColumn>()
                        .Select(c => c.ColumnName)
                        .Where(cn =>
                            !columnasPrioritarias.Contains(cn == "VIAB_HUMANO" ? "CRONIDOL" :
                                cn == "VIAB_VETERINARIO" ? "SOS" : cn) && !columnasOcultas.Contains(cn) && cn != "NOMBRE_EMPRESA")
                );
                dtReordenado = dt.DefaultView.ToTable(false, nuevasColumnas.ToArray());
            }
            
            // Actualizar variables de paginación
            filasFiltradas = dtReordenado.Rows.Cast<DataRow>().ToList();
            totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
            
            // Mostrar solo la página actual
            int inicio = (paginaActual - 1) * FilasPorPagina;
            int fin = Math.Min(inicio + FilasPorPagina, dtReordenado.Rows.Count);
            
            // Crear tabla solo con las filas de la página actual
            var dtPaginado = dtReordenado.Clone();
            for (int i = inicio; i < fin; i++)
            {
                dtPaginado.ImportRow(dtReordenado.Rows[i]);
            }

            var borderColor = Avalonia.Media.Brushes.Gray;
            var brushConverter = new Avalonia.Media.BrushConverter();
            var headerBackground = (Avalonia.Media.IBrush?)brushConverter.ConvertFrom("#E5F0FF") ??
                                   Avalonia.Media.Brushes.LightGray; // Pale blue header
            var evenRowBackground = Avalonia.Media.Brushes.White; // White even rows
            var oddRowBackground = (Avalonia.Media.IBrush?)brushConverter.ConvertFrom("#F0F6FF") ??
                                   Avalonia.Media.Brushes.Gainsboro; // Light blue odd rows
            var hoverBackground = (Avalonia.Media.IBrush?)brushConverter.ConvertFrom("#D6E8FF") ??
                                  Avalonia.Media.Brushes.LightBlue;
            var successBorder = Avalonia.Media.Brushes.LimeGreen;
            var errorBorder = Avalonia.Media.Brushes.Red;

            var tableGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };

            // Define columns
            for (int i = 0; i < dtPaginado.Columns.Count; i++)
                tableGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            // Define rows (header + data rows)
            tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Header row
            for (int i = 0; i < dtPaginado.Rows.Count; i++)
                tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            // Header cells
            for (int col = 0; col < dtPaginado.Columns.Count; col++)
            {
                var headerCell = new Border
                {
                    BorderBrush = borderColor,
                    BorderThickness = new Thickness(0, 0, 0, 2), // Thicker bottom border
                    Background = headerBackground,
                    Padding = new Thickness(12, 10, 12, 10),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                headerCell.Child = new TextBlock
                {
                    Text = GetDisplayName(dtPaginado.Columns[col].ColumnName, entidad),
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    FontSize = 15,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                tableGrid.Children.Add(headerCell);
                Grid.SetRow(headerCell, 0);
                Grid.SetColumn(headerCell, col);
            }

            // Data cells with zebra striping and hover effect
            for (int row = 0; row < dtPaginado.Rows.Count; row++)
            {
                var rowBackground = (row % 2 == 0) ? evenRowBackground : oddRowBackground;
                for (int col = 0; col < dtPaginado.Columns.Count; col++)
                {
                    int capturedRow = row + inicio; // Ajustar para la paginación
                    int capturedCol = col;
                    Border cellBorder = new Border
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(0.5),
                        Background = rowBackground,
                        Padding = new Thickness(12, 8, 12, 8),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    string columnName = dtPaginado.Columns[col].ColumnName;
                    
                                                    // Lógica específica para Clientes
                    if (entidad == "Clientes")
                    {
                        if (columnName == "RESPUESTA")
                        {
                            var opciones = new[] { "Cambiar a vacío", "🟢 Aceptado", "🟡 Pendiente", "🔴 Rechazado" };
                            var valorActual = dtPaginado.Rows[row][col]?.ToString() ?? "";
                            
                            var comboBox = new ComboBox
                            {
                                ItemsSource = opciones,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                Padding = new Thickness(8, 4),
                                FontSize = 14
                            };
                            
                            if (string.IsNullOrEmpty(valorActual))
                            {
                                comboBox.SelectedItem = null;
                            }
                            else if (valorActual.Contains("Aceptado"))
                            {
                                comboBox.SelectedItem = "🟢 Aceptado";
                            }
                            else if (valorActual.Contains("Pendiente"))
                            {
                                comboBox.SelectedItem = "🟡 Pendiente";
                            }
                            else if (valorActual.Contains("Rechazado"))
                            {
                                comboBox.SelectedItem = "🔴 Rechazado";
                            }
                            else
                            {
                                comboBox.SelectedItem = valorActual;
                            }
                            
                            comboBox.SelectionChanged += (s, e) =>
                            {
                                var selected = comboBox.SelectedItem?.ToString();
                                if (selected == "Cambiar a vacío")
                                {
                                    if (_lastDataTable != null && capturedRow < _lastDataTable.Rows.Count)
                                    {
                                        _lastDataTable.Rows[capturedRow][capturedCol] = "";
                                        comboBox.SelectedItem = null;
                                        // Guardar en la base de datos
                                        GuardarCelda(_lastDataTable.Rows[capturedRow], _lastDataTable.Columns[capturedCol].ColumnName, "");
                                    }
                                }
                                else if (!string.IsNullOrEmpty(selected))
                                {
                                    if (_lastDataTable != null && capturedRow < _lastDataTable.Rows.Count)
                                    {
                                        _lastDataTable.Rows[capturedRow][capturedCol] = selected;
                                        // Guardar en la base de datos
                                        GuardarCelda(_lastDataTable.Rows[capturedRow], _lastDataTable.Columns[capturedCol].ColumnName, selected);
                                    }
                                }
                            };
                            
                            cellBorder.Child = comboBox;
                            continue; // Saltar al siguiente campo
                        }
                    }
                    
                    if (columnName == "VERIFICADO")
                    {
                        bool isChecked = false;
                        var value = dtPaginado.Rows[row][col];
                        if (value != null && int.TryParse(value.ToString(), out int v))
                            isChecked = v == 1;
                        var checkBox = new CheckBox
                        {
                            IsChecked = isChecked,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        };
                        checkBox.IsCheckedChanged += async (s, e) =>
                        {
                            int newValue = checkBox.IsChecked == true ? 1 : 0;
                            dtReordenado.Rows[capturedRow][capturedCol] = newValue;
                            await TryUpdateCell(dtReordenado, capturedRow, capturedCol,
                                new TextBox { Text = newValue.ToString() }, cellBorder, successBorder, errorBorder,
                                rowBackground);
                        };
                        // Centrado visual sin fondo especial
                        var grid = new Grid
                        {
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        };
                        grid.Children.Add(checkBox);
                        cellBorder.Child = grid;
                    }


                    else if (columnName == "EMAIL" && entidad == "Clientes")
                    {
                        // Crear hipervínculo para email
                        var emailText = dtPaginado.Rows[row][col]?.ToString() ?? "";
                        var hyperlink = new TextBlock
                        {
                            Text = emailText,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            Foreground = Avalonia.Media.Brushes.Blue,
                            TextDecorations = Avalonia.Media.TextDecorations.Underline,
                            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                        };
                        
                        hyperlink.PointerPressed += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(emailText))
                            {
                                try
                                {
                                    var psi = new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = $"mailto:{emailText}",
                                        UseShellExecute = true
                                    };
                                    System.Diagnostics.Process.Start(psi);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error opening email: {ex.Message}");
                                }
                            }
                        };
                        
                        cellBorder.Child = hyperlink;
                    }
                    else if (entidad == "Clientes Asociados" && columnName == "ID_DISTRIBUIDOR")
                    {
                        var id = dtPaginado.Rows[row][col]?.ToString() ?? "";
                        var nombreDistribuidor = GetDistribuidores().FirstOrDefault(d => d.Id == id).Nombre ?? id;
                        cellBorder.Child = new TextBlock
                        {
                            Text = string.IsNullOrEmpty(nombreDistribuidor) ? id : nombreDistribuidor,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                        };
                    }
                    else if (col == 0)
                    {
                        cellBorder.Child = new TextBlock
                        {
                            Text = dtPaginado.Rows[row][col]?.ToString() ?? "",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                        };
                    }
                    else if (columnName == "FECHA_PAGO")
                    {
                        // Condición específica para FECHA_PAGO - prioridad máxima
                        cellBorder.Child = new TextBlock
                        {
                            Text = dtPaginado.Rows[row][col]?.ToString() ?? "",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            Foreground = Avalonia.Media.Brushes.DarkGray // Color gris oscuro para indicar que no es editable
                        };
                    }
                    else if (_camposNoEditables.Contains(columnName) || columnName.Contains("FECHA") || columnName.Contains("HORA") || columnName.Contains("TIMESTAMP"))
                    {
                        // Otros campos no editables
                        Console.WriteLine($"DEBUG: Campo no editable detectado: {columnName}");
                        cellBorder.Child = new TextBlock
                        {
                            Text = dtPaginado.Rows[row][col]?.ToString() ?? "",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            Foreground = Avalonia.Media.Brushes.DarkGray
                        };
                    }
                    else // Editable cell (todos los demás campos son editables)
                    {
                        var tb = new TextBox
                        {
                            Text = dtPaginado.Rows[row][col]?.ToString() ?? "",
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Background = rowBackground
                        };

                        bool esNoEditable = _camposNoEditables.Contains(columnName.ToUpperInvariant());

                        if (esNoEditable)
                        {
                            tb.IsReadOnly = true;
                            tb.Foreground = Avalonia.Media.Brushes.DimGray;
                            tb.Background = rowBackground;
                        }
                        else
                        {
                            tb.LostFocus += async (s, e) =>
                            {
                                await TryUpdateCell(dtReordenado, capturedRow, capturedCol, tb, cellBorder,
                                    successBorder, errorBorder, rowBackground);
                            };
                            tb.KeyDown += async (s, e) =>
                            {
                                if (e.Key == Avalonia.Input.Key.Enter)
                                {
                                    await TryUpdateCell(dtReordenado, capturedRow, capturedCol, tb, cellBorder,
                                        successBorder, errorBorder, rowBackground);
                                    e.Handled = true;
                                }
                            };
                        }
                        cellBorder.Child = tb;
                    }

                    // Row hover effect: only attach to the first cell in the row
                    if (col == 0)
                    {
                        int rowIndex = row; // capture for closure
                        cellBorder.PointerEntered += (s, e) =>
                        {
                            for (int c = 0; c < dtPaginado.Columns.Count; c++)
                            {
                                var border =
                                    tableGrid.Children[(rowIndex + 1) * dtPaginado.Columns.Count + c] as Border;
                                if (border != null) border.Background = hoverBackground;
                            }
                        };
                        cellBorder.PointerExited += (s, e) =>
                        {
                            var bg = (rowIndex % 2 == 0) ? evenRowBackground : oddRowBackground;
                            for (int c = 0; c < dtPaginado.Columns.Count; c++)
                            {
                                var border =
                                    tableGrid.Children[(rowIndex + 1) * dtPaginado.Columns.Count + c] as Border;
                                if (border != null) border.Background = bg;
                            }
                        };
                    }

                    tableGrid.Children.Add(cellBorder);
                    Grid.SetRow(cellBorder, row + 1);
                    Grid.SetColumn(cellBorder, col);
                }
            }

            // Wrap the table in a Border for rounded corners and shadow
            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                BoxShadow = new Avalonia.Media.BoxShadows(new Avalonia.Media.BoxShadow
                {
                    Blur = 8,
                    Color = Avalonia.Media.Color.FromArgb(25, 0, 0, 0), // 10% opacity black
                    OffsetX = 0,
                    OffsetY = 2
                }),
                Background = Avalonia.Media.Brushes.White,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 8, 0, 8),
                Child = tableGrid
            };

            panel.Children.Add(outerBorder);
            
            // Actualizar controles de paginación
            var paginaTextBox = this.FindControl<TextBox>("PaginaActualTextBox");
            if (paginaTextBox != null)
                paginaTextBox.Text = paginaActual.ToString();
            
            var totalPaginasText = this.FindControl<TextBlock>("TotalPaginasText");
            if (totalPaginasText != null)
                totalPaginasText.Text = $"/ {totalPaginas}";
            
            var btnAnt = this.FindControl<Button>("AnteriorPaginaButton");
            if (btnAnt != null)
                btnAnt.IsEnabled = paginaActual > 1;
            
            var btnSig = this.FindControl<Button>("SiguientePaginaButton");
            if (btnSig != null)
                btnSig.IsEnabled = paginaActual < totalPaginas;
        }

        // Helper to update the database and show feedback
        private async System.Threading.Tasks.Task TryUpdateCell(DataTable dt, int row, int col, TextBox textBox,
            Border cellBorder, Avalonia.Media.IBrush successBorder, Avalonia.Media.IBrush errorBorder,
            Avalonia.Media.IBrush rowBackground)
        {
            // Verificar índices válidos
            if (row < 0 || row >= dt.Rows.Count || col < 0 || col >= dt.Columns.Count)
            {
                MostrarMensaje($"Error: Índices de fila o columna fuera de rango. Row: {row}, Col: {col}, Total Rows: {dt.Rows.Count}, Total Cols: {dt.Columns.Count}", true);
                return;
            }
            
            string newValue = textBox.Text ?? "";
            string columnName = dt.Columns[col].ColumnName;
            

            
            try
            {
                
                // No permitir edición de campos de fecha específicos
                if (columnName == "FECHA_PAGO" || columnName.Contains("FECHA") || columnName.Contains("HORA") || columnName.Contains("TIMESTAMP"))
                {
                    return; // Salir sin hacer nada para campos de fecha
                }
                
                // Determinar la tabla y el ID según la entidad actual
                string tabla = "";
                string idColumn = "";
                string idValue = "";
                
                switch (_selectedEntity)
                {
                    case "Clientes":
                        tabla = "CLIENTE";
                        idColumn = "ID_CLIENTE";
                        break;
                    case "Pedido":
                        tabla = "PEDIDO";
                        idColumn = "ID_PEDIDO";
                        break;
                    case "Pago":
                        tabla = "PAGO";
                        idColumn = "ID_CLIENTE, ID_TRANSACCION"; // Clave primaria compuesta
                        break;
                    case "Empleados":
                        tabla = "EMPLEADO";
                        idColumn = "ID_EMPLEADO";
                        break;
                    case "Clientes Asociados":
                        tabla = "CLIENTE_ASOCIADO";
                        idColumn = "ID_CLIENTE_ASOCIADO";
                        break;
                    default:
                        MostrarMensaje("Error: Entidad no soportada para edición", true);
                        return;
                }
                
                // Verificar que existe la columna ID
                if (!dt.Columns.Contains(idColumn))
                {
                    MostrarMensaje($"Error: Columna ID no encontrada para {_selectedEntity}", true);
                    return;
                }
                
                // Verificar que la fila existe antes de acceder
                if (row >= dt.Rows.Count)
                {
                    MostrarMensaje($"Error: Fila {row} no existe en el DataTable. Total filas: {dt.Rows.Count}", true);
                    return;
                }
                
                idValue = dt.Rows[row][idColumn]?.ToString() ?? "";
                
                if (string.IsNullOrEmpty(idValue))
                {
                    MostrarMensaje($"Error: ID no válido para {_selectedEntity}", true);
                    return;
                }
                
                // Update the database
                if (_dao != null)
                {
                    string sql;
                    var parameters = new Dictionary<string, object>();
                    
                    // Manejar clave primaria compuesta para PAGO
                    string whereClause;
                    if (_selectedEntity == "Pago")
                    {
                        // Para PAGO, usar clave primaria compuesta: ID_CLIENTE, ID_TRANSACCION
                        string idCliente = dt.Rows[row]["ID_CLIENTE"]?.ToString() ?? "";
                        string idTransaccion = dt.Rows[row]["ID_TRANSACCION"]?.ToString() ?? "";
                        whereClause = "ID_CLIENTE = :idCliente AND ID_TRANSACCION = :idTransaccion";
                        parameters["idCliente"] = idCliente;
                        parameters["idTransaccion"] = idTransaccion;
                    }
                    else
                    {
                        // Para otras entidades, usar clave primaria simple
                        whereClause = $"{idColumn} = :id";
                        parameters["id"] = idValue;
                    }

                    EnsureSafeUpdateTarget(tabla, columnName, idColumn);
                    
                    // Para campos de fecha, usar TO_TIMESTAMP_TZ para convertir string a TIMESTAMP WITH TIME ZONE
                    if (columnName.Contains("FECHA") || columnName.Contains("HORA"))
                    {
                        if (!string.IsNullOrEmpty(newValue))
                        {
                            sql = $"UPDATE {tabla} SET {columnName} = TO_TIMESTAMP_TZ(:value, 'DD/MM/YYYY HH24:MI:SS TZH:TZM') WHERE {whereClause}";
                            parameters["value"] = newValue;
                        }
                        else
                        {
                            sql = $"UPDATE {tabla} SET {columnName} = NULL WHERE {whereClause}";
                        }
                    }
                    else
                    {
                        // Para campos normales
                        sql = $"UPDATE {tabla} SET {columnName} = :value WHERE {whereClause}";
                        parameters["value"] = string.IsNullOrEmpty(newValue) ? DBNull.Value : (object)newValue;
                    }
                    
                    _dao.ExecuteNonQuery(sql, parameters);
                }

                // Actualizar el DataTable como string
                dt.Rows[row][col] = string.IsNullOrEmpty(newValue) ? DBNull.Value : (object)newValue;
                cellBorder.BorderBrush = successBorder;
                await System.Threading.Tasks.Task.Delay(800);
                cellBorder.BorderBrush = Avalonia.Media.Brushes.Gray;
                cellBorder.Background = rowBackground;
            }
            catch (IndexOutOfRangeException ex)
            {
                MostrarMensaje($"Error de índice fuera de rango: {ex.Message}", true);
                cellBorder.BorderBrush = errorBorder;
                await System.Threading.Tasks.Task.Delay(1200);
                cellBorder.BorderBrush = Avalonia.Media.Brushes.Gray;
                cellBorder.Background = rowBackground;
            }
            catch (Exception ex)
            {
                // Silenciar errores para campos de fecha
                if (!(columnName.Contains("FECHA") || columnName.Contains("HORA") || columnName.Contains("TIMESTAMP")))
                {
                    MostrarMensaje($"Error al actualizar celda: {ex.Message}", true);
                }
                cellBorder.BorderBrush = errorBorder;
                await System.Threading.Tasks.Task.Delay(1200);
                cellBorder.BorderBrush = Avalonia.Media.Brushes.Gray;
                cellBorder.Background = rowBackground;
            }
        }

        private async void ExportToExcel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_lastDataTable == null || _lastDataTable.Rows.Count == 0)
            {
                MostrarMensaje("No hay datos para exportar.");
                return;
            }

            var options = new FilePickerSaveOptions
            {
                Title = "Exportar a Excel",
                SuggestedFileName = $"{_selectedEntity}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel")
                    {
                        Patterns = new[] { "*.xlsx" },
                        MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }
                    }
                }
            };
            var storageProvider = this.StorageProvider;
            if (storageProvider == null)
            {
                MostrarMensaje("No se pudo acceder al sistema de archivos.", true);
                return;
            }

            var file = await storageProvider.SaveFilePickerAsync(options);
            if (file == null) return;
            try
            {
                using var stream = await file.OpenWriteAsync();
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add(_selectedEntity);
                // Headers
                for (int col = 0; col < _lastDataTable.Columns.Count; col++)
                    ws.Cell(1, col + 1).Value = _lastDataTable.Columns[col].ColumnName;
                // Data
                for (int row = 0; row < _lastDataTable.Rows.Count; row++)
                for (int col = 0; col < _lastDataTable.Columns.Count; col++)
                    ws.Cell(row + 2, col + 1).Value = _lastDataTable.Rows[row][col]?.ToString() ?? string.Empty;
                workbook.SaveAs(stream);
                MostrarMensaje($"Exportado correctamente a: {file.Name}");
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error exportando: {ex.Message}", true);
            }
        }

        public void MostrarMensaje(string mensaje, bool esError = false)
        {
            var dialog = new Window
            {
                Title = esError ? "Error" : "Mensaje",
                Content = new TextBlock { Text = mensaje },
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dialog.ShowDialog(this);
        }

        public void MostrarError(string mensaje)
        {
            MostrarMensaje(mensaje, true);
        }

        private void SetBuscarIcon(string entity)
        {
            var btnClientes = this.FindControl<Button>("BuscarClientesButton");
            var btnPedido = this.FindControl<Button>("BuscarPedidoButton");
            var btnPago = this.FindControl<Button>("BuscarPagoButton");
            var btnEmpleados = this.FindControl<Button>("BuscarEmpleadosButton");
            if (btnClientes == null || btnPedido == null || btnPago == null || btnEmpleados == null) return;
            btnClientes.IsVisible = entity == "Clientes";
            btnPedido.IsVisible = entity == "Pedido";
            btnPago.IsVisible = entity == "Pago";
            btnEmpleados.IsVisible = entity == "Empleados";
        }

        private void SetResultadosTemplate(string entity)
        {
            var listBox = this.FindControl<ListBox>("ResultadosListBox");
            var cabeceraClientes = this.FindControl<Grid>("CabeceraClientes");
            var cabeceraPedido = this.FindControl<Grid>("CabeceraPedido");
            var cabeceraPago = this.FindControl<Grid>("CabeceraPago");
            var cabeceraEmpleados = this.FindControl<Grid>("CabeceraEmpleados");
            if (listBox == null) return;
            if (cabeceraClientes != null) cabeceraClientes.IsVisible = entity == "Clientes";
            if (cabeceraPedido != null) cabeceraPedido.IsVisible = entity == "Pedido";
            if (cabeceraPago != null) cabeceraPago.IsVisible = entity == "Pago";
            if (cabeceraEmpleados != null) cabeceraEmpleados.IsVisible = entity == "Empleados";
            switch (entity)
            {
                case "Clientes":
                    listBox.ItemTemplate = new FuncDataTemplate<Cliente>((item, _) =>
                    {
                        var panel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Background = item.AlternatingColor,
                            Margin = new Thickness(0),
                            Spacing = 0
                        };
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.ID_CLIENTE, Width = 120,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.NOMBRE_CLIENTE, Width = 150,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.NOMBRE_CONTACTO, Width = 120,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.APELLIDO_CONTACTO, Width = 120,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.LINEA_DIRECCION1, Width = 180,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.LINEA_DIRECCION2, Width = 180,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.CIUDAD, Width = 100,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.REGION, Width = 100,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.PAIS, Width = 100, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.CODIGO_POSTAL, Width = 100,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.VIABILIDAD, Width = 100,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.VIAB_HUMANO, Width = 80,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.VIAB_VETERINARIO, Width = 80,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.FACT_H, Width = 80,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.AFIN_H, Width = 80,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.PUNTUAC_H, Width = 80,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.FACT_V, Width = 80,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.AFIN_V, Width = 80,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.PUNTUAC_V, Width = 80,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.PUESTO, Width = 80,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.CAMPO, Width = 100,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.EMAIL, Width = 150,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.VERIFICADO, Width = 120,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.COMERCIAL, Width = 120,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.RESPUESTA, Width = 120,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.CATEGORIA, Width = 120,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.COMENTARIOS, Width = 120,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        panel.Children.Add(new TextBlock
                        {
                            Text = item.TELEFONO, Width = 120,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Thickness(4, 0)
                        });
                        return panel;
                    }, true);
                    break;
                case "Pedido":
                    listBox.ItemTemplate = new FuncDataTemplate<Pedido>((item, _) =>
                    {
                        var tbCodigo = new TextBox
                            { Text = ((Pedido)item).Codigo, Width = 100, Margin = new Thickness(2, 0) };
                        tbCodigo.KeyDown += Celda_KeyDown;
                        tbCodigo.LostFocus += Celda_LostFocus;
                        var tbFecha = new TextBox
                            { Text = ((Pedido)item).Fecha, Width = 120, Margin = new Thickness(2, 0) };
                        tbFecha.KeyDown += Celda_KeyDown;
                        tbFecha.LostFocus += Celda_LostFocus;
                        var tbEstado = new TextBox
                            { Text = ((Pedido)item).Estado, Width = 100, Margin = new Thickness(2, 0) };
                        tbEstado.KeyDown += Celda_KeyDown;
                        tbEstado.LostFocus += Celda_LostFocus;
                        return new Border
                        {
                            Background = ((Pedido)item).AlternatingColor,
                            Child = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(0),
                                Children =
                                {
                                    tbCodigo,
                                    tbFecha,
                                    tbEstado
                                }
                            }
                        };
                    }, true);
                    break;
                case "Pago":
                    listBox.ItemTemplate = new FuncDataTemplate<Pago>((item, _) =>
                    {
                        var tbImporte = new TextBox
                            { Text = ((Pago)item).Importe, Width = 100, Margin = new Thickness(2, 0) };
                        tbImporte.KeyDown += Celda_KeyDown;
                        tbImporte.LostFocus += Celda_LostFocus;
                        var tbFecha = new TextBox
                            { Text = ((Pago)item).Fecha, Width = 120, Margin = new Thickness(2, 0) };
                        tbFecha.KeyDown += Celda_KeyDown;
                        tbFecha.LostFocus += Celda_LostFocus;
                        var tbMetodo = new TextBox
                            { Text = ((Pago)item).Metodo, Width = 100, Margin = new Thickness(2, 0) };
                        tbMetodo.KeyDown += Celda_KeyDown;
                        tbMetodo.LostFocus += Celda_LostFocus;
                        return new Border
                        {
                            Background = ((Pago)item).AlternatingColor,
                            Child = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(0),
                                Children =
                                {
                                    tbImporte,
                                    tbFecha,
                                    tbMetodo
                                }
                            }
                        };
                    }, true);
                    break;
                case "Empleados":
                    listBox.ItemTemplate = new FuncDataTemplate<Empleado>((item, _) =>
                    {
                        var tbNombre = new TextBox
                            { Text = ((Empleado)item).Nombre, Width = 150, Margin = new Thickness(2, 0) };
                        tbNombre.KeyDown += Celda_KeyDown;
                        tbNombre.LostFocus += Celda_LostFocus;
                        var tbPuesto = new TextBox
                            { Text = ((Empleado)item).Puesto, Width = 120, Margin = new Thickness(2, 0) };
                        tbPuesto.KeyDown += Celda_KeyDown;
                        tbPuesto.LostFocus += Celda_LostFocus;
                        var tbEmail = new TextBox
                            { Text = ((Empleado)item).Email, Width = 200, Margin = new Thickness(2, 0) };
                        tbEmail.KeyDown += Celda_KeyDown;
                        tbEmail.LostFocus += Celda_LostFocus;
                        return new Border
                        {
                            Background = ((Empleado)item).AlternatingColor,
                            Child = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(0),
                                Children =
                                {
                                    tbNombre,
                                    tbPuesto,
                                    tbEmail
                                }
                            }
                        };
                    }, true);
                    break;
                default:
                    listBox.ItemTemplate = null;
                    break;
            }
        }

        // Handler para edición en línea (Enter, LostFocus)
        private void Celda_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && sender is TextBox tb)
            {
                tb.BorderBrush = Avalonia.Media.Brushes.LimeGreen;
                tb.BorderThickness = new Avalonia.Thickness(2);
            }
        }

        private void Celda_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.BorderBrush = Avalonia.Media.Brushes.Gray;
                tb.BorderThickness = new Avalonia.Thickness(1);
            }
        }

        private void MessageBox(string msg)
        {
            var dlg = new Window
            {
                Title = "Debug",
                Content = new TextBlock { Text = msg, Margin = new Thickness(20) },
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dlg.ShowDialog(this);
        }

        private void InsertarCliente_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Open InsertarClienteWindow as dialog, passing _dao and this
            var insertarWindow = new InsertarClienteWindow(_dao!, this);
            insertarWindow.ShowDialog(this);
            // After closing, refresh the client list
            MostrarTodosClientes();
        }

        private void InsertarPedido_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                if (_dao == null)
                {
                    MostrarMensaje("Error: No hay conexión a la base de datos", true);
                    return;
                }
                
                var win = new InsertarPedidoWindow(_dao, this);
                win.PedidoInsertado += () =>
                {
                    try
                    {
                        MostrarTodosPedidos();
                    }
                    catch (Exception ex)
                    {
                        MostrarMensaje($"Error al actualizar la tabla: {ex.Message}", true);
                    }
                };
                win.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al abrir la ventana de inserción: {ex.Message}", true);
            }
        }

        private void InsertarPago_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var win = new InsertarPagoWindow(_dao!, this);
            win.ShowDialog(this);
            MostrarTodosPagos();
        }

        private void InsertarEmpleado_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var win = new InsertarEmpleadoWindow(_dao!, this);
            win.ShowDialog(this);
            
            // Limpiar cache de empleados después de insertar uno nuevo
            ClearEmpleadosCache();
            
            MostrarTodosEmpleados();
        }

        private void InsertarClienteAsociado_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_dao == null) return;
            var win = new InsertarClienteAsociadoWindow(_dao, this);
            win.ClienteAsociadoInsertado += () => MostrarTodosClientesAsociados();
            win.ShowDialog(this);
        }

        // Método auxiliar para reordenar columnas de Cliente
        private DataTable ReordenarColumnasCliente(DataTable dt)
        {
            var columnas = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            Console.WriteLine($"[DEBUG] Columnas originales: {string.Join(", ", columnas)}");
            
            var columnasPrioritarias = new[] { "VERIFICADO", "COMERCIAL", "CRONIDOL", "SOS", "RESPUESTA" };
            var columnasReordenadas = new List<string>();
            foreach (var col in columnasPrioritarias)
            {
                if (col == "CRONIDOL" && columnas.Contains("VIAB_HUMANO"))
                    columnasReordenadas.Add("VIAB_HUMANO");
                else if (col == "SOS" && columnas.Contains("VIAB_VETERINARIO"))
                    columnasReordenadas.Add("VIAB_VETERINARIO");
                else if (columnas.Contains(col))
                {
                    columnasReordenadas.Add(col);
                    // Insertar COMENTARIOS justo después de RESPUESTA
                    if (col == "RESPUESTA" && columnas.Contains("COMENTARIOS") && !columnasReordenadas.Contains("COMENTARIOS"))
                    {
                        columnasReordenadas.Add("COMENTARIOS");
                        Console.WriteLine("[DEBUG] COMENTARIOS insertado después de RESPUESTA");
                    }
                }
            }

            var columnasOcultas = new[]
                { "VIABILIDAD", "FACT_H", "AFIN_H", "PUNTUAC_H", "FACT_V", "AFIN_V", "PUNTUAC_V" };
            var resto = columnas.Where(col =>
                !columnasReordenadas.Contains(col) && !columnasOcultas.Contains(col) && col != "TELEFONO" &&
                col != "EMAIL" && col != "NOMBRE_CONTACTO" && col != "APELLIDO_CONTACTO" && col != "NOMBRE_EMPRESA" &&
                col != "NOMBRE_CLIENTE" && col != "DISTRIBUIDOR" && col != "COMENTARIOS").ToList();
            // Insertar NOMBRE_CLIENTE y NOMBRE_EMPRESA solo si existen y no están ya en la lista
            if (columnas.Contains("NOMBRE_CLIENTE") && !columnasReordenadas.Contains("NOMBRE_CLIENTE"))
                columnasReordenadas.Add("NOMBRE_CLIENTE");
            if (columnas.Contains("NOMBRE_EMPRESA") && !columnasReordenadas.Contains("NOMBRE_EMPRESA"))
                columnasReordenadas.Add("NOMBRE_EMPRESA");
            if (columnas.Contains("DISTRIBUIDOR"))
                columnasReordenadas.Add("DISTRIBUIDOR");
            if (columnas.Contains("EMAIL"))
                columnasReordenadas.Add("EMAIL");
            if (columnas.Contains("NOMBRE_CONTACTO"))
                columnasReordenadas.Add("NOMBRE_CONTACTO");
            if (columnas.Contains("APELLIDO_CONTACTO"))
                columnasReordenadas.Add("APELLIDO_CONTACTO");
            columnasReordenadas.AddRange(resto);
            if (columnas.Contains("TELEFONO"))
                columnasReordenadas.Add("TELEFONO");
            // Si NOMBRE_EMPRESA no existe, agrégalo como columna vacía (pero solo si no está ya)
            if (!columnas.Contains("NOMBRE_EMPRESA") && !columnasReordenadas.Contains("NOMBRE_EMPRESA"))
            {
                var dt2 = dt.DefaultView.ToTable(false, columnasReordenadas.ToArray());
                dt2.Columns.Add("NOMBRE_EMPRESA");
                foreach (DataRow row in dt2.Rows)
                    row["NOMBRE_EMPRESA"] = string.Empty;
                int idx = dt2.Columns.Contains("NOMBRE_CLIENTE") ? dt2.Columns["NOMBRE_CLIENTE"]!.Ordinal + 1 : 0;
                dt2.Columns["NOMBRE_EMPRESA"]!.SetOrdinal(idx);
                return dt2;
            }

            Console.WriteLine($"[DEBUG] Columnas reordenadas final: {string.Join(", ", columnasReordenadas)}");
            return dt.DefaultView.ToTable(false, columnasReordenadas.ToArray());
        }

        private void MostrarTodosClientes()
        {
            Console.WriteLine("[DEBUG] MostrarTodosClientes - Iniciando");
            if (_dao == null)
            {
                Console.WriteLine("[DEBUG] MostrarTodosClientes - _dao es null");
                return;
            }
            try
            {
                var dt = _dao.ExecuteQuery("SELECT * FROM CLIENTE", new Dictionary<string, object>());
                var dtReordenado = ReordenarColumnasCliente(dt);
                _lastDataTable = dtReordenado;
                _lastEntidad = "Clientes";
                filasFiltradas = dtReordenado.Rows.Cast<DataRow>().ToList();
                paginaActual = 1;
                totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                Console.WriteLine($"[DEBUG] MostrarTodosClientes - Datos obtenidos: {dtReordenado?.Rows.Count ?? 0} filas");
                if (dtReordenado != null)
                {
                    MostrarTablaPaginada(dtReordenado, "Clientes");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] MostrarTodosClientes - Error: {ex.Message}");
                var panel = this.FindControl<StackPanel>("TablaResultado");
                if (panel != null)
                {
                    panel.Children.Clear();
                    panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                }
            }
        }

        private void MostrarTodosClientesAsociados()
        {
            Console.WriteLine("[DEBUG] MostrarTodosClientesAsociados - Iniciando");
            if (_dao == null)
            {
                Console.WriteLine("[DEBUG] MostrarTodosClientesAsociados - _dao es null");
                return;
            }
            try
            {
                var dt = _dao.ExecuteQuery("SELECT * FROM CLIENTE_ASOCIADO", new Dictionary<string, object>());
                _lastDataTable = dt;
                _lastEntidad = "Clientes Asociados";
                filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                paginaActual = 1;
                totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                Console.WriteLine($"[DEBUG] MostrarTodosClientesAsociados - Datos obtenidos: {dt?.Rows.Count ?? 0} filas");
                if (dt != null)
                {
                    MostrarTablaPaginada(dt, "Clientes Asociados");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] MostrarTodosClientesAsociados - Error: {ex.Message}");
                var panel = this.FindControl<StackPanel>("TablaResultado");
                if (panel != null)
                {
                    panel.Children.Clear();
                    panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                }
            }
        }

        private void DatoTextBox_KeyDown_BuscarClienteAsociadoId(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var datoTextBox = sender as TextBox;
                if (datoTextBox == null || string.IsNullOrWhiteSpace(datoTextBox.Text)) return;
                if (_dao == null) return;
                try
                {
                    string input = datoTextBox.Text.Trim();
                    string sql;
                    var parameters = new Dictionary<string, object>();
                    
                    // Buscar tanto por ID_CLIENTE_ASOCIADO exacto como por nombre de cliente
                    // ID_CLIENTE_ASOCIADO es VARCHAR2, por lo que puede ser cualquier string
                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        sql = "SELECT * FROM CLIENTE_ASOCIADO WHERE UPPER(ID_CLIENTE_ASOCIADO) = :id OR LOWER(NOMBRE_CLIENTE_ASOCIADO) LIKE :nombre";
                        parameters["id"] = input.ToUpper();
                        parameters["nombre"] = $"%{input.ToLower()}%";
                    }
                    else
                    {
                        // Si el input está vacío, mostrar todos
                        sql = "SELECT * FROM CLIENTE_ASOCIADO";
                    }
                    
                    var dt = _dao.ExecuteQuery(sql, parameters);
                    _lastDataTable = dt;
                    _lastEntidad = "Clientes Asociados";
                    paginaActual = 1;
                    filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaPaginada(dt, "Clientes Asociados");
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        private void DatoTextBox_KeyDown_BuscarClienteAsociadoDistribuidor(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var datoTextBox = sender as TextBox;
                if (datoTextBox == null || string.IsNullOrWhiteSpace(datoTextBox.Text)) return;
                if (_dao == null) return;
                try
                {
                    string input = datoTextBox.Text.Trim();
                    // Buscar tanto por ID del distribuidor como por nombre
                    string sql = @" SELECT ca.*, d.NOMBRE_DISTRIBUIDOR
                                    FROM CLIENTE_ASOCIADO ca
                                    LEFT JOIN DISTRIBUIDOR d ON ca.ID_DISTRIBUIDOR = d.ID_DISTRIBUIDOR
                                    WHERE UPPER(ca.ID_DISTRIBUIDOR) LIKE :distribuidor
                                    OR UPPER(d.NOMBRE_DISTRIBUIDOR) LIKE :distribuidor";
                    var parameters = new Dictionary<string, object> { ["distribuidor"] = $"%{input.ToUpper()}%" };
                    var dt = _dao.ExecuteQuery(sql, parameters);
                    _lastDataTable = dt;
                    _lastEntidad = "Clientes Asociados";
                    paginaActual = 1;
                    filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaPaginada(dt, "Clientes Asociados");
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        private void DatoTextBox_KeyDown_BuscarEmpleadoId(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var datoTextBox = sender as TextBox;
                if (datoTextBox == null || string.IsNullOrWhiteSpace(datoTextBox.Text)) return;
                if (_dao == null) return;
                try
                {
                    string input = datoTextBox.Text.Trim();
                    string sql;
                    var parameters = new Dictionary<string, object>();
                    
                    // Buscar tanto por ID_EMPLEADO exacto como por nombre de empleado
                    // ID_EMPLEADO es VARCHAR2(50), por lo que puede ser cualquier string
                    sql = "SELECT * FROM EMPLEADO WHERE ID_EMPLEADO = :id OR LOWER(NOMBRE) LIKE :nombre";
                    parameters["id"] = input;
                    parameters["nombre"] = $"%{input.ToLower()}%";
                    var dt = _dao.ExecuteQuery(sql, parameters);
                    _lastDataTable = dt;
                    _lastEntidad = "Empleados";
                    _selectedEntity = "Empleados";
                    paginaActual = 1;
                    filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaPaginada(dt, "Empleados");
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        private void DatoTextBox_KeyDown_BuscarEmpleadoNombre(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                var datoTextBox = sender as TextBox;
                if (datoTextBox == null || string.IsNullOrWhiteSpace(datoTextBox.Text)) return;
                if (_dao == null) return;
                try
                {
                    string input = datoTextBox.Text.Trim();
                    string sql;
                    var parameters = new Dictionary<string, object>();
                    
                    // Buscar tanto por ID_EMPLEADO exacto como por nombre de empleado
                    // ID_EMPLEADO es VARCHAR2(50), por lo que puede ser cualquier string
                    sql = "SELECT * FROM EMPLEADO WHERE ID_EMPLEADO = :id OR LOWER(NOMBRE) LIKE :nombre";
                    parameters["id"] = input;
                    parameters["nombre"] = $"%{input.ToLower()}%";
                    var dt = _dao.ExecuteQuery(sql, parameters);
                    _lastDataTable = dt;
                    _lastEntidad = "Empleados";
                    _selectedEntity = "Empleados";
                    paginaActual = 1;
                    filasFiltradas = dt.Rows.Cast<DataRow>().ToList();
                    totalPaginas = (int)Math.Ceiling((double)filasFiltradas.Count / FilasPorPagina);
                    MostrarTablaComoVisual(dt, "Empleados");
                }
                catch (Exception ex)
                {
                    var panel = this.FindControl<StackPanel>("TablaResultado");
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        panel.Children.Add(new TextBlock { Text = $"Error: {ex.Message}" });
                    }
                }
            }
        }

        private void ActualizarTablaPaginada()
        {
            // Refresh the current paginated table for the selected entity
            if (_lastDataTable == null) return;
            MostrarTablaPaginada(_lastDataTable, _lastEntidad);
        }

        private async void BorrarClienteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_lastEntidad != "Clientes" || _lastDataTable == null)
            {
                MostrarMensaje("No hay cliente seleccionado.", true);
                return;
            }

            var panel = this.FindControl<StackPanel>("TablaResultado");
            if (panel == null)
            {
                MostrarMensaje("No hay cliente seleccionado.", true);
                return;
            }

            int? selectedRow = _selectedClienteRow;
            if (!selectedRow.HasValue || selectedRow.Value < 0)
            {
                MostrarMensaje("Selecciona un cliente para borrar.", true);
                return;
            }

            DataTable dtSorted = _lastDataTable;
            if (!string.IsNullOrEmpty(_sortColumn) && _lastDataTable.Columns.Contains(_sortColumn))
            {
                var dv = new DataView(_lastDataTable);
                dv.Sort = $"[{_sortColumn}] {(_sortDescending ? "DESC" : "ASC")}";
                dtSorted = dv.ToTable();
            }

            int inicio = (paginaActual - 1) * FilasPorPagina;
            int fin = Math.Min(inicio + FilasPorPagina, dtSorted.Rows.Count);
            int visualRow = inicio + selectedRow.Value;
            if (visualRow < 0 || visualRow >= dtSorted.Rows.Count)
            {
                MostrarMensaje("No hay cliente seleccionado.", true);
                return;
            }

            var idCol = dtSorted.Columns.Contains("ID_CLIENTE") ? "ID_CLIENTE" : null;
            if (idCol == null)
            {
                MostrarMensaje("No se pudo obtener el ID del cliente.", true);
                return;
            }

            var id = _selectedClienteId;
            if (string.IsNullOrWhiteSpace(id))
            {
                MostrarMensaje("No hay cliente seleccionado.", true);
                return;
            }

            var confirm = await MessageBoxConfirm($"¿Seguro que deseas borrar el cliente con ID {id}?");
            if (!confirm) return;
            try
            {
                if (_dao != null)
                {
                    string sql = $"DELETE FROM CLIENTE WHERE ID_CLIENTE = :id";
                    var parameters = new Dictionary<string, object> { ["id"] = id };
                    _dao.ExecuteNonQuery(sql, parameters);
                    MostrarMensaje($"Cliente {id} borrado.");
                    _selectedClienteRow = null;
                    _selectedClienteId = null;
                    MostrarTodosClientes();
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al borrar: {ex.Message}", true);
            }
        }

        private async Task<bool> MessageBoxConfirm(string msg)
        {
            var dlg = new Window
            {
                Title = "Confirmar",
                Width = 320,
                Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock { Text = msg, FontSize = 15, Margin = new Thickness(0, 0, 0, 10) },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Spacing = 16,
                            Children =
                            {
                                new Button { Content = "Sí", Width = 80, IsDefault = true },
                                new Button { Content = "No", Width = 80, IsCancel = true }
                            }
                        }
                    }
                }
            };
            var buttons = ((dlg.Content as StackPanel)?.Children[1] as StackPanel)?.Children;
            bool result = false;
            if (buttons != null && buttons.Count == 2)
            {
                (buttons[0] as Button)!.Click += (_, __) =>
                {
                    result = true;
                    dlg.Close();
                };
                (buttons[1] as Button)!.Click += (_, __) =>
                {
                    result = false;
                    dlg.Close();
                };
            }

            await dlg.ShowDialog(this);
            return result;
        }

        private bool AnyTextBoxHasFocus()
        {
            var panel = this.FindControl<StackPanel>("TablaResultado");
            if (panel == null) return false;
            foreach (var tb in GetAllTextBoxes(panel))
            {
                if (tb.IsFocused)
                {
                    // Guardar info de edición para restaurar tras refresco
                    var row = tb.DataContext as DataRow;
                    if (row != null && _lastDataTable != null && _lastDataTable.Columns.Contains("ID_CLIENTE"))
                    {
                        _editingClienteId = row["ID_CLIENTE"]?.ToString();
                        _editingCol = tb.Name;
                        _editingText = tb.Text;
                        _editingCaret = tb.CaretIndex;
                    }

                    return true;
                }
            }

            return false;
        }

        private IEnumerable<TextBox> GetAllTextBoxes(object parent)
        {
            if (parent is TextBox tb)
                yield return tb;
            if (parent is Avalonia.Controls.Controls controls)
            {
                foreach (var child in controls)
                {
                    foreach (var t in GetAllTextBoxes(child))
                        yield return t;
                }
            }
        }
    

        private void PaginaActualTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
                CambiarPaginaDesdeTextBox();
        }

        private void PaginaActualTextBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            CambiarPaginaDesdeTextBox();
        }

        private void CambiarPaginaDesdeTextBox()
        {
            var paginaTextBox = this.FindControl<TextBox>("PaginaActualTextBox");
            if (paginaTextBox == null) return;
            if (int.TryParse(paginaTextBox.Text, out int nuevaPagina))
            {
                if (nuevaPagina >= 1 && nuevaPagina <= totalPaginas)
                {
                    paginaActual = nuevaPagina;
                    ActualizarTablaPaginada();
                }
                else
                {
                    MostrarMensaje($"Página fuera de rango (1-{totalPaginas})", true);
                    paginaTextBox.Text = paginaActual.ToString();
                }
            }
            else
            {
                MostrarMensaje("Introduce un número de página válido", true);
                paginaTextBox.Text = paginaActual.ToString();
            }
            var totalPaginasText = this.FindControl<TextBlock>("TotalPaginasText");
            if (totalPaginasText != null)
                totalPaginasText.Text = $"/ {totalPaginas}";
        }

        private List<(string Id, string Nombre)> GetEmpleados()
        {
            if (_empleadosCache != null) return _empleadosCache;
            RefreshEmpleadosCache();
            return _empleadosCache;
        }

        private void RefreshEmpleadosCache()
        {
            _empleadosCache = new List<(string, string)>();
            if (_dao != null)
            {
                try
                {
                    var dt = _dao.ExecuteQuery("SELECT ID_EMPLEADO, NOMBRE FROM EMPLEADO ORDER BY NOMBRE", new Dictionary<string, object>());
                    foreach (DataRow row in dt.Rows)
                    {
                        var id = row["ID_EMPLEADO"]?.ToString() ?? "";
                        var nombre = row["NOMBRE"]?.ToString() ?? "";
                        _empleadosCache.Add((id, nombre));
                    }
                    Console.WriteLine($"[DEBUG] Empleados cache actualizado: {_empleadosCache.Count} empleados cargados");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error al cargar empleados: {ex.Message}");
                    _empleadosCache = new List<(string, string)>();
                }
            }
        }

        public void ClearEmpleadosCache()
        {
            _empleadosCache = null;
            Console.WriteLine("[DEBUG] Cache de empleados limpiado");
        }

        private void RefreshButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("[DEBUG] Botón refrescar clickeado");
                
                // Limpiar cache de empleados
                ClearEmpleadosCache();
                
                // Si estamos mostrando clientes, refrescar la tabla
                if (_selectedEntity == "Clientes" && _lastDataTable != null)
                {
                    Console.WriteLine("[DEBUG] Refrescando tabla de clientes");
                    MostrarTablaPaginada(_lastDataTable, "Clientes");
                    MostrarMensaje("Datos refrescados correctamente", false);
                }
                else
                {
                    MostrarMensaje("No hay datos para refrescar", true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error al refrescar: {ex.Message}");
                MostrarMensaje($"Error al refrescar: {ex.Message}", true);
            }
        }

        private void ShowRefreshButton(bool show)
        {
            var refreshButton = this.FindControl<Button>("RefreshButton");
            if (refreshButton != null)
            {
                refreshButton.IsVisible = show;
            }
        }
    }

// Modelos de ejemplo para binding (puedes moverlos a archivos separados si lo prefieres)
public class Cliente
{
    public string? ID_CLIENTE { get; set; }
    public string? NOMBRE_CLIENTE { get; set; }
    public string? NOMBRE_CONTACTO { get; set; }
    public string? APELLIDO_CONTACTO { get; set; }
    public string? LINEA_DIRECCION1 { get; set; }
    public string? LINEA_DIRECCION2 { get; set; }
    public string? CIUDAD { get; set; }
    public string? REGION { get; set; }
    public string? PAIS { get; set; }
    public string? CODIGO_POSTAL { get; set; }
    public string? VIABILIDAD { get; set; }
    public string? VIAB_HUMANO { get; set; }
    public string? VIAB_VETERINARIO { get; set; }
    public string? FACT_H { get; set; }
    public string? AFIN_H { get; set; }
    public string? PUNTUAC_H { get; set; }
    public string? FACT_V { get; set; }
    public string? AFIN_V { get; set; }
    public string? PUNTUAC_V { get; set; }
    public string? PUESTO { get; set; }
    public string? CAMPO { get; set; }
    public string? EMAIL { get; set; }
    public string? VERIFICADO { get; set; }
    public string? COMERCIAL { get; set; }
    public string? RESPUESTA { get; set; }
    public string? CATEGORIA { get; set; }
    public string? COMENTARIOS { get; set; }
    public string? TELEFONO { get; set; }
    public Avalonia.Media.IBrush? AlternatingColor { get; set; }
}

public class Pedido
{
    public string? Codigo { get; set; }
    public string? Fecha { get; set; }
    public string? Estado { get; set; }
    public Avalonia.Media.IBrush? AlternatingColor { get; set; }
}

public class Pago
{
    public string? Importe { get; set; }
    public string? Fecha { get; set; }
    public string? Metodo { get; set; }
    public Avalonia.Media.IBrush? AlternatingColor { get; set; }
}

public class Empleado
{
    public string? Nombre { get; set; }
    public string? Puesto { get; set; }
    public string? Email { get; set; }
    public Avalonia.Media.IBrush? AlternatingColor { get; set; }
}
}
