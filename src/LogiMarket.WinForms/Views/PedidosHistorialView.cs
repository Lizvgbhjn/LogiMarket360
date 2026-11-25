namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Interpreter;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Ventas;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class PedidosHistorialView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly ErrorProvider _errores = new();
    private readonly OperacionesPedido _operaciones;
    private readonly IFormularioPrincipalMediator _mediator;

    private TextBox _txtConsulta = null!;
    private DataGridView _gridPedidos = null!;
    private Panel _panelTimeline = null!;
    private Label _lblTimelineEstado = null!;
    private Label _lblSeguimiento = null!;
    private readonly ToolTip _historialPuntos = new();
    private Label[] _timelinePuntos = null!;

    public PedidosHistorialView(ConsultasOperativas consultas, OperacionesPedido operaciones, IFormularioPrincipalMediator mediator)
    {
        _operaciones = operaciones;
        _mediator = mediator;
        _consultas = consultas;
        ValidacionCampo.Configurar(this, _errores);
        InicializarComponentes();
    }

    private async Task CancelarAsync()
    {
        var codigo = _gridPedidos.CurrentRow?.Cells["Codigo"].Value?.ToString();
        if (string.IsNullOrEmpty(codigo)) return;
        var pedido = await _consultas.PedidoPorCodigoAsync(codigo);
        if (pedido == null) return;
        if (MessageBox.Show($"¿Cancelar el pedido {codigo}?", "Pedidos",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        try
        {
            var resultado = await _operaciones.CancelarAsync(pedido.Id, "Cancelado desde el historial");
            MessageBox.Show(resultado.Mensaje, "Pedidos");
            if (resultado.Exitoso)
            {
                await _mediator.NotificarOperacionAsync("PEDIDO_CONFIRMADO", pedido.Id);
                await RecargarAsync();
            }
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Pedidos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void InicializarComponentes()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 250, 252);
        AutoScroll = true;

        var lblTitulo = new Label { Text = "Pedidos e historial", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Consulta de estados, detalle y trazabilidad de eventos", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        // Buscador e Interpreter
        var lblBuscar = new Label { Text = "Consulta / Filtro gramatical (Interpreter):", Location = new Point(25, 90), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        _txtConsulta = new TextBox { Location = new Point(25, 115), Size = new Size(420, 32), Font = new Font("Segoe UI", 10), Text = "ESTADO = PAGADO AND TOTAL >= 50" };

        var btnFiltrar = new Button
        {
            Text = "Interpretar filtro",
            Location = new Point(455, 114),
            Size = new Size(130, 32),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnFiltrar.Click += async (s, e) => await FiltrarConInterpreterAsync();

        var btnTodos = new Button { Text = "TODOS", Location = new Point(595, 114), Size = new Size(80, 32), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
        btnTodos.Click += async (s, e) => { _txtConsulta.Text = ""; await RecargarAsync(); };

        var btnBorrador = new Button { Text = "BORRADOR", Location = new Point(680, 114), Size = new Size(95, 32), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
        btnBorrador.Click += (s, e) => { _txtConsulta.Text = "ESTADO = BORRADOR"; btnFiltrar.PerformClick(); };

        var btnPagado = new Button { Text = "PAGADO", Location = new Point(780, 114), Size = new Size(85, 32), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
        btnPagado.Click += (s, e) => { _txtConsulta.Text = "ESTADO = PAGADO"; btnFiltrar.PerformClick(); };

        var btnEntregado = new Button { Text = "ENTREGADO", Location = new Point(870, 114), Size = new Size(100, 32), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
        btnEntregado.Click += (s, e) => { _txtConsulta.Text = "ESTADO = ENTREGADO"; btnFiltrar.PerformClick(); };

        var btnCancelar = new Button { Text = "Cancelar pedido", Location = new Point(978, 114), Size = new Size(147, 32) };
        btnCancelar.Click += async (_, _) => await CancelarAsync();
        Controls.Add(btnCancelar);

        // DataGridView
        _gridPedidos = new DataGridView
        {
            Location = new Point(25, 160),
            Size = new Size(1100, 380),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        _gridPedidos.Columns.Add("Codigo", "Código");
        _gridPedidos.Columns.Add("Cliente", "Cliente");
        _gridPedidos.Columns.Add("Estado", "Estado");
        _gridPedidos.Columns.Add("Total", "Total");
        _gridPedidos.Columns.Add("Fecha", "Fecha");
        _gridPedidos.SelectionChanged += OnSeleccionPedido;

        // Timeline de estados del pedido seleccionado
        _panelTimeline = new Panel
        {
            Location = new Point(25, 555),
            Size = new Size(1100, 120),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblTimelineEstado = new Label
        {
            Text = "Historial seleccionado: Ninguno",
            Location = new Point(20, 15),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        var nombresEstados = new[] { "BORRADOR", "VALIDADO", "PAGADO", "DESPACHADO", "ENTREGADO" };
        _timelinePuntos = new Label[nombresEstados.Length];

        for (int i = 0; i < nombresEstados.Length; i++)
        {
            var lblEtiqueta = new Label
            {
                Text = nombresEstados[i],
                Location = new Point(200 + (i * 180), 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            var lblPunto = new Label
            {
                Text = "●",
                Location = new Point(230 + (i * 180), 45),
                AutoSize = true,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(203, 213, 225)
            };

            _timelinePuntos[i] = lblPunto;
            _panelTimeline.Controls.AddRange(new Control[] { lblEtiqueta, lblPunto });
        }

        _panelTimeline.Controls.Add(_lblTimelineEstado);
        _lblSeguimiento = new Label { Location = new Point(20, 91), AutoSize = true, AccessibleName = "Seguimiento del pedido" };
        _panelTimeline.Controls.Add(_lblSeguimiento);
        Disposed += (_, _) => _historialPuntos.Dispose();

        Controls.AddRange(new Control[] {
            lblTitulo, lblSubtitulo, lblBuscar, _txtConsulta, btnFiltrar, btnTodos, btnBorrador, btnPagado, btnEntregado,
            _gridPedidos, _panelTimeline
        });
    }

    public async Task RecargarAsync()
    {
        var pedidos = await _consultas.PedidosAsync();
        LlenarGrid(pedidos);
    }

    private void LlenarGrid(IEnumerable<Pedido> pedidos)
    {
        _gridPedidos.Rows.Clear();
        foreach (var p in pedidos)
        {
            var fila = _gridPedidos.Rows.Add(p.Codigo, p.Cliente?.Nombre ?? "N/A", p.Estado.ToString(), $"USD {p.Total:F2}", p.FechaCreacion.ToString("dd/MM/yyyy HH:mm"));
            _gridPedidos.Rows[fila].Tag = p;
        }

        if (_gridPedidos.Rows.Count > 0)
        {
            _gridPedidos.Rows[0].Selected = true;
            ActualizarTimeline();
        }
    }

    private async Task FiltrarConInterpreterAsync()
    {
        _errores.Clear();
        var query = _txtConsulta.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            await RecargarAsync();
            return;
        }

        try
        {
            // Ejecutar el parser del patrón Interpreter
            var expresion = FiltroPedidosParser.Parse(query);
            var todos = await _consultas.PedidosAsync();
            var filtrados = todos.Where(p => expresion.Interpretar(p)).ToList();

            LlenarGrid(filtrados);
        }
        catch (SintaxisFiltroInvalidaException ex)
        {
            ValidacionCampo.Mostrar(_errores, _txtConsulta, $"{ex.Message} Ejemplo: ESTADO = PAGADO AND TOTAL >= 50.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inesperado: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSeleccionPedido(object? sender, EventArgs e)
    {
        ActualizarTimeline();
    }

    private void ActualizarTimeline()
    {
        if (_gridPedidos.CurrentRow == null) return;
        var codigo = _gridPedidos.CurrentRow.Cells["Codigo"].Value?.ToString() ?? "";
        var estadoStr = _gridPedidos.CurrentRow.Cells["Estado"].Value?.ToString() ?? "";

        _lblTimelineEstado.Text = $"Historial seleccionado: {codigo} ({estadoStr})";
        var pedido = _gridPedidos.CurrentRow.Tag as Pedido;
        if (pedido == null) return;
        var despacho = pedido.Despacho;
        _lblSeguimiento.Text = despacho == null
            ? "Seguimiento: aún no hay guía registrada."
            : $"Seguimiento: {despacho.GuiaSeguimiento} · {despacho.EstadoDespacho} · estimada {despacho.FechaEntregaEstimada:dd/MM/yyyy}"
                + (despacho.FechaEntregaReal is DateTime fecha ? $" · entregado {fecha:dd/MM/yyyy HH:mm}" : "");
        DateTime?[] fechas = [pedido.FechaCreacion, null, pedido.FechaPago, pedido.FechaDespacho, pedido.FechaEntrega];
        for (var i = 0; i < fechas.Length; i++)
            _historialPuntos.SetToolTip(_timelinePuntos[i], fechas[i]?.ToString("dd/MM/yyyy HH:mm") ?? "Sin fecha registrada");

        var orden = new[] { "BORRADOR", "VALIDADO", "PAGADO", "DESPACHADO", "ENTREGADO" };
        var indiceActual = Array.IndexOf(orden, estadoStr.ToUpperInvariant());

        for (int i = 0; i < _timelinePuntos.Length; i++)
        {
            if (indiceActual >= 0 && i <= indiceActual)
            {
                _timelinePuntos[i].ForeColor = Color.FromArgb(44, 62, 80); // Activo / completado
            }
            else
            {
                _timelinePuntos[i].ForeColor = Color.FromArgb(203, 213, 225); // Inactivo
            }
        }
    }
}
