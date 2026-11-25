namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Memento;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Ventas;
using LogiMarket.Application.Prototype;
using LogiMarket.Domain.Composite;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class CarritoBorradorView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly ErrorProvider _errores = new();
    private readonly GestionVentas _ventas;
    private readonly IFormularioPrincipalMediator _mediator;
    private ComposicionCatalogo? _catalogo;
    private Pedido? _cotizacion;
    private bool _recargando;
    private sealed record OpcionCliente(Cliente Cliente)
    {
        public override string ToString() => $"{Cliente.Nombre} ({Cliente.Categoria})";
    }

    // Patrón Memento
    private HistorialBorradorCaretaker _caretaker = new();
    private Pedido? _pedidoBorradorActual;

    private ComboBox _cbClientes = null!;
    private ComboBox _cbTipoEnvio = null!;
    private DataGridView _gridCarrito = null!;
    private Button _btnDeshacer = null!;
    private Button _btnRehacer = null!;
    private Button _btnQuitarLinea = null!;
    private Label _lblSubtotal = null!;
    private Label _lblDescuento = null!;
    private Label _lblEntrega = null!;
    private Label _lblTotal = null!;
    private Button _btnValidar = null!;

    public CarritoBorradorView(
        ConsultasOperativas consultas, GestionVentas ventas,
        IFormularioPrincipalMediator mediator)
    {
        _ventas = ventas;
        _mediator = mediator;
        _consultas = consultas;
        ValidacionCampo.Configurar(this, _errores);
        InicializarComponentes();
    }

    private void InicializarComponentes()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 250, 252);
        AutoScroll = true;

        var lblTitulo = new Label { Text = "Carrito y borrador", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Selección de cliente, líneas de pedido y control de cambios", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        // Controles de cliente y entrega
        var lblCli = new Label { Text = "Cliente:", Location = new Point(25, 90), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        _cbClientes = new ComboBox { Location = new Point(25, 115), Size = new Size(260, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };
        _cbClientes.SelectedIndexChanged += (s, e) => RecalcularTotales();

        var lblEnvio = new Label { Text = "Forma de Entrega:", Location = new Point(295, 90), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        _cbTipoEnvio = new ComboBox { Location = new Point(295, 115), Size = new Size(180, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };
        _cbTipoEnvio.Items.Add("ESTÁNDAR");
        _cbTipoEnvio.Items.Add("EXPRÉS");
        _cbTipoEnvio.SelectedIndex = 0;
        _cbTipoEnvio.SelectedIndexChanged += (s, e) => RecalcularTotales();

        var btnAddProd = new Button { Text = "Añadir producto", Location = new Point(485, 114), Size = new Size(130, 32), BackColor = Color.FromArgb(44, 62, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnAddProd.Click += async (s, e) => await AnadirProductoAsync();

        var btnAddKit = new Button { Text = "Añadir kit", Location = new Point(625, 114), Size = new Size(100, 32), BackColor = Color.FromArgb(44, 62, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnAddKit.Click += async (s, e) => await AnadirKitAsync();

        // DataGridView del Carrito
        _gridCarrito = new DataGridView
        {
            Location = new Point(25, 160),
            Size = new Size(700, 390),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        _gridCarrito.Columns.Add("Articulo", "Artículo");
        _gridCarrito.Columns.Add("Cantidad", "Cantidad");
        _gridCarrito.Columns.Add("PrecioUnitario", "P. unitario");
        _gridCarrito.Columns.Add("Importe", "Importe");

        // Botones Deshacer, Rehacer, Quitar línea, Clonar
        _btnDeshacer = new Button { Text = "↶ Deshacer", Location = new Point(25, 565), Size = new Size(110, 35), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f), Enabled = false };
        _btnDeshacer.Click += (s, e) => DeshacerMemento();

        _btnRehacer = new Button { Text = "↷ Rehacer", Location = new Point(145, 565), Size = new Size(110, 35), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f), Enabled = false };
        _btnRehacer.Click += (s, e) => RehacerMemento();

        _btnQuitarLinea = new Button { Text = "✕ Quitar línea", Location = new Point(265, 565), Size = new Size(120, 35), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f) };
        _btnQuitarLinea.Click += (s, e) => QuitarLinea();

        var btnClonar = new Button { Text = "❐ Clonar borrador", Location = new Point(395, 565), Size = new Size(140, 35), BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f) };
        btnClonar.Click += (s, e) => ClonarBorradorPrototype();

        // Panel Lateral: Resumen
        var panelResumen = new Panel
        {
            Location = new Point(745, 115),
            Size = new Size(385, 485),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblResumenTitulo = new Label { Text = "Resumen del pedido", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 20), AutoSize = true };

        var lblSub = new Label { Text = "Subtotal:", Location = new Point(20, 75), AutoSize = true, Font = new Font("Segoe UI", 10) };
        _lblSubtotal = new Label { Text = "USD 0.00", Location = new Point(220, 75), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        var lblDesc = new Label { Text = "Descuento aplicado:", Location = new Point(20, 120), AutoSize = true, Font = new Font("Segoe UI", 10) };
        _lblDescuento = new Label { Text = "- USD 0.00", Location = new Point(220, 120), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(22, 163, 74) };

        var lblEnt = new Label { Text = "Tarifa de entrega:", Location = new Point(20, 165), AutoSize = true, Font = new Font("Segoe UI", 10) };
        _lblEntrega = new Label { Text = "—", Location = new Point(220, 165), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        var lblSeparador = new Label { Text = "--------------------------------------------------------", Location = new Point(20, 200), AutoSize = true, ForeColor = Color.FromArgb(203, 213, 225) };

        var lblTot = new Label { Text = "TOTAL:", Location = new Point(20, 230), AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
        _lblTotal = new Label { Text = "—", Location = new Point(200, 230), AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };

        _btnValidar = new Button
        {
            Text = "Validar y guardar pedido",
            Location = new Point(20, 310),
            Size = new Size(345, 45),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnValidar.Click += async (s, e) => await GuardarYValidarPedidoAsync();

        panelResumen.Controls.AddRange(new Control[] {
            lblResumenTitulo, lblSub, _lblSubtotal, lblDesc, _lblDescuento, lblEnt, _lblEntrega,
            lblSeparador, lblTot, _lblTotal, _btnValidar
        });

        Controls.AddRange(new Control[] {
            lblTitulo, lblSubtitulo, lblCli, _cbClientes, lblEnvio, _cbTipoEnvio, btnAddProd, btnAddKit,
            _gridCarrito, _btnDeshacer, _btnRehacer, _btnQuitarLinea, btnClonar, panelResumen
        });
        AccesibilidadVista.Reacomodar(this, _gridCarrito, panelResumen, _btnQuitarLinea);
    }

    public async Task RecargarAsync()
    {
        var seleccionado = (_cbClientes.SelectedItem as OpcionCliente)?.Cliente.Id;
        var clientes = await _consultas.ClientesAsync();
        _catalogo = new ComposicionCatalogo(await _consultas.ProductosAsync(), await _consultas.KitsAsync());
        _recargando = true;
        try
        {
            _cbClientes.Items.Clear();
            foreach (var cliente in clientes.Where(c => c.Activo))
            {
                var indice = _cbClientes.Items.Add(new OpcionCliente(cliente));
                if (cliente.Id == seleccionado)
                    _cbClientes.SelectedIndex = indice;
            }
        }
        finally
        {
            _recargando = false;
        }

        if (_pedidoBorradorActual == null)
            InicializarNuevoBorrador();
        else
            RecalcularTotales();
    }

    private void InicializarNuevoBorrador()
    {
        var codigo = $"PED-{Guid.NewGuid():N}";
        _pedidoBorradorActual = new Pedido(codigo, 0, TipoEnvio.ESTANDAR);
        _caretaker = new HistorialBorradorCaretaker();
        ActualizarEstadoBotonesMemento();
        ActualizarGrid();
        RecalcularTotales();
    }

    private async Task AnadirProductoAsync()
    {
        var prods = await _consultas.ProductosAsync();
        if (!prods.Any()) return;

        var items = prods.Select(p => $"{p.Sku} | {p.Nombre} | USD {p.Precio:F2}").ToArray();
        var seleccionado = MostrarSelector("Seleccione un producto:", items);
        if (string.IsNullOrEmpty(seleccionado)) return;

        var sku = seleccionado.Split('|')[0].Trim();
        var prod = prods.First(p => p.Sku == sku);

        // Guardar estado en Memento antes de modificar
        _caretaker.GuardarEstado(_pedidoBorradorActual!);

        var linea = _pedidoBorradorActual!.Lineas.FirstOrDefault(l => l.ArticuloCodigo == prod.Sku);
        if (linea != null)
        {
            _pedidoBorradorActual!.CambiarCantidad(linea, linea.Cantidad + 1);
        }
        else
        {
            _pedidoBorradorActual.AgregarLinea(new PedidoDetalle(prod.Sku, prod.Nombre, false, 1, prod.Precio));
        }

        ActualizarGrid();
        RecalcularTotales();
        ActualizarEstadoBotonesMemento();
    }

    private async Task AnadirKitAsync()
    {
        var kits = await _consultas.KitsAsync();
        if (!kits.Any()) return;

        var items = kits.Where(k => k.Activo && k.Componentes.Count > 0).Select(k => $"{k.Codigo} | {k.Nombre}").ToArray();
        var seleccionado = MostrarSelector("Seleccione un kit:", items);
        if (string.IsNullOrEmpty(seleccionado)) return;

        var codigo = seleccionado.Split('|')[0].Trim();
        var kit = kits.First(k => k.Codigo == codigo);


        _caretaker.GuardarEstado(_pedidoBorradorActual!);

        var linea = _pedidoBorradorActual!.Lineas.FirstOrDefault(l => l.ArticuloCodigo == kit.Codigo);
        if (linea != null)
        {
            _pedidoBorradorActual!.CambiarCantidad(linea, linea.Cantidad + 1);
        }
        else
        {
            _pedidoBorradorActual.AgregarLinea(new PedidoDetalle(kit.Codigo, kit.Nombre, true, 1, 0m));
        }

        ActualizarGrid();
        RecalcularTotales();
        ActualizarEstadoBotonesMemento();
    }

    private void QuitarLinea()
    {
        if (_gridCarrito.CurrentRow == null || _pedidoBorradorActual == null) return;
        var codigo = _gridCarrito.CurrentRow.Cells["Articulo"].Value?.ToString();
        if (string.IsNullOrEmpty(codigo)) return;

        _caretaker.GuardarEstado(_pedidoBorradorActual);

        var linea = _pedidoBorradorActual.Lineas.FirstOrDefault(l => l.ArticuloCodigo == codigo);
        if (linea != null)
        {
            _pedidoBorradorActual.QuitarLinea(linea);
        }

        ActualizarGrid();
        RecalcularTotales();
        ActualizarEstadoBotonesMemento();
    }

    private void DeshacerMemento()
    {
        if (_pedidoBorradorActual != null && _caretaker.PuedeDeshacer)
        {
            _caretaker.Deshacer(_pedidoBorradorActual);
            ActualizarGrid();
            RecalcularTotales();
            ActualizarEstadoBotonesMemento();
        }
    }

    private void RehacerMemento()
    {
        if (_pedidoBorradorActual != null && _caretaker.PuedeRehacer)
        {
            _caretaker.Rehacer(_pedidoBorradorActual);
            ActualizarGrid();
            RecalcularTotales();
            ActualizarEstadoBotonesMemento();
        }
    }

    private void ActualizarEstadoBotonesMemento()
    {
        _btnDeshacer.Enabled = _caretaker.PuedeDeshacer;
        _btnRehacer.Enabled = _caretaker.PuedeRehacer;
    }

    private void ClonarBorradorPrototype()
    {
        if (_pedidoBorradorActual == null || !_pedidoBorradorActual.Lineas.Any())
        {
            MessageBox.Show("Añada al menos una línea antes de clonar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var clon = PedidoBorradorPrototype.Clonar(_pedidoBorradorActual);
        _pedidoBorradorActual = clon;
        _caretaker.GuardarEstado(_pedidoBorradorActual);
        ActualizarGrid();
        RecalcularTotales();
        ActualizarEstadoBotonesMemento();

        MessageBox.Show($"Borrador clonado mediante patrón Prototype.\nNuevo código: {clon.Codigo}\nReferencias independientes de colecciones garantizadas.", "Prototype Exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ActualizarGrid()
    {
        _gridCarrito.Rows.Clear();
        if (_pedidoBorradorActual == null) return;

        foreach (var l in (_cotizacion ?? _pedidoBorradorActual).Lineas)
        {
            _gridCarrito.Rows.Add(l.ArticuloCodigo, l.Cantidad, $"USD {l.PrecioUnitario:F2}", $"USD {l.Subtotal:F2}");
        }
    }

    private void RecalcularTotales()
    {
        if (_recargando || _pedidoBorradorActual == null) return;
        _errores.Clear();
        _cotizacion = null;
        _lblSubtotal.Text = _lblDescuento.Text = _lblEntrega.Text = _lblTotal.Text = "—";

        if (_cbClientes.SelectedItem is OpcionCliente opcion && _catalogo != null
            && _pedidoBorradorActual.Lineas.Count > 0)
        {
            try
            {
                var envio = _cbTipoEnvio.SelectedIndex == 1 ? TipoEnvio.EXPRES : TipoEnvio.ESTANDAR;
                _cotizacion = _ventas.Cotizar(_pedidoBorradorActual, opcion.Cliente, envio, _catalogo);
                _lblSubtotal.Text = $"USD {_cotizacion.Subtotal:F2}";
                _lblDescuento.Text = $"- USD {_cotizacion.Descuento:F2}";
                _lblEntrega.Text = $"USD {_cotizacion.TarifaEnvio:F2}";
                _lblTotal.Text = $"USD {_cotizacion.Total:F2}";
            }
            catch (LogiMarket.Domain.Exceptions.DomainException error)
            {
                _errores.SetError(_gridCarrito, error.Message);
            }
        }

        _btnValidar.Enabled = _cotizacion != null && _ventas.PuedeModificar;
        ActualizarGrid();
    }

    private async Task GuardarYValidarPedidoAsync()
    {
        if (_cotizacion == null)
        {
            ValidacionCampo.Mostrar(_errores, _cbClientes.SelectedItem == null ? _cbClientes : _gridCarrito, "Seleccione un cliente y agregue líneas válidas.");
            return;
        }

        _btnValidar.Enabled = false;
        try
        {
            var cotizacion = _cotizacion;
            await _ventas.GuardarPedidoValidadoAsync(cotizacion, cotizacion.ClienteId);

            _caretaker.BloquearYLimpiar();
            ActualizarEstadoBotonesMemento();

            MessageBox.Show($"Pedido {cotizacion.Codigo} guardado en estado VALIDADO.\nListo para proceder al cobro.", "Pedido Validado", MessageBoxButtons.OK, MessageBoxIcon.Information);

            InicializarNuevoBorrador();
            await _mediator.NotificarOperacionAsync("PEDIDO_CONFIRMADO");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al validar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RecalcularTotales();
        }
    }

    private static string MostrarSelector(string titulo, string[] opciones)
    {
        using var form = new Form { Width = 450, Height = 280, Text = titulo, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
        var list = new ListBox { Left = 20, Top = 20, Width = 390, Height = 150, Font = new Font("Segoe UI", 10) };
        list.Items.AddRange(opciones);
        if (list.Items.Count > 0) list.SelectedIndex = 0;

        var btnOk = new Button { Text = "Seleccionar", Left = 210, Top = 185, Width = 100, Height = 35, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(44, 62, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnCancel = new Button { Text = "Cancelar", Left = 320, Top = 185, Width = 90, Height = 35, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };

        form.Controls.AddRange(new Control[] { list, btnOk, btnCancel });
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        return form.ShowDialog() == DialogResult.OK ? list.SelectedItem?.ToString() ?? "" : "";
    }
}
