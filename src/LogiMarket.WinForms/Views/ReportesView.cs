namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class ReportesView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly ErrorProvider _errores = new();
    private readonly IEmisorComprobantes _emisor;
    private readonly IFormularioPrincipalMediator _mediator;

    private DateTimePicker _dtpDesde = null!;
    private DateTimePicker _dtpHasta = null!;
    private Label _lblPedidosConfirmados = null!;
    private Label _lblVentasNetas = null!;
    private Label _lblDevoluciones = null!;
    private ComboBox _cbPedidos = null!;
    private TextBox _txtVistaPrevia = null!;
    private Label _lblRutaArchivo = null!;

    public ReportesView(ConsultasOperativas consultas, IEmisorComprobantes emisor, IFormularioPrincipalMediator mediator)
    {
        _emisor = emisor;
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

        var lblTitulo = new Label { Text = "Reportes y comprobantes", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Consulta, formatos de presentación y medios de salida (Patrón Bridge)", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        // Filtros de fecha
        var lDesde = new Label { Text = "Desde:", Location = new Point(25, 95), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _dtpDesde = new DateTimePicker { Location = new Point(25, 120), Size = new Size(160, 30), Font = new Font("Segoe UI", 9.5f), Format = DateTimePickerFormat.Short, Value = DateTime.UtcNow.AddMonths(-1) };

        var lHasta = new Label { Text = "Hasta:", Location = new Point(205, 95), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _dtpHasta = new DateTimePicker { Location = new Point(205, 120), Size = new Size(160, 30), Font = new Font("Segoe UI", 9.5f), Format = DateTimePickerFormat.Short, Value = DateTime.UtcNow };

        var btnConsultar = new Button
        {
            Text = "Consultar",
            Location = new Point(385, 119),
            Size = new Size(110, 32),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        btnConsultar.Click += async (s, e) => await RecargarAsync();

        // 3 Cajas KPI
        var panelKpis = new Panel { Location = new Point(25, 165), Size = new Size(1095, 80), BackColor = Color.Transparent };

        var card1 = CrearKpi("Pedidos confirmados", out _lblPedidosConfirmados, 0);
        var card2 = CrearKpi("Ventas netas", out _lblVentasNetas, 370);
        var card3 = CrearKpi("Devoluciones", out _lblDevoluciones, 740);
        panelKpis.Controls.AddRange(new Control[] { card1, card2, card3 });

        // Selector de pedido
        var lSelPed = new Label { Text = "Seleccionar pedido para comprobante:", Location = new Point(25, 260), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _cbPedidos = new ComboBox { Location = new Point(25, 285), Size = new Size(550, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10) };

        // Acciones Bridge
        var btnVistaPreviaVenta = new Button { Text = "Vista previa (Venta)", Location = new Point(590, 284), Size = new Size(160, 32), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f) };
        btnVistaPreviaVenta.Click += async (s, e) => await EmitirComprobanteAsync(false, false);
        var btnVistaPreviaDevolucion = new Button
        {
            Text = "Vista previa (Devolución)", Location = new Point(590, 319), Size = new Size(193, 30),
            AccessibleName = "Vista previa del comprobante de devolución"
        };
        btnVistaPreviaDevolucion.Click += async (_, _) => await EmitirComprobanteAsync(true, false);
        Controls.Add(btnVistaPreviaDevolucion);

        var btnTxtVenta = new Button { Text = "Exportar TXT (Venta)", Location = new Point(760, 284), Size = new Size(160, 32), BackColor = Color.FromArgb(44, 62, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        btnTxtVenta.Click += async (s, e) => await EmitirComprobanteAsync(false, true);

        var btnTxtDevolucion = new Button { Text = "Exportar TXT (Devolución)", Location = new Point(930, 284), Size = new Size(190, 32), BackColor = Color.FromArgb(71, 85, 105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f) };
        btnTxtDevolucion.Click += async (s, e) => await EmitirComprobanteAsync(true, true);

        // Card Vista Previa del Documento
        var panelPreview = new Panel
        {
            Location = new Point(25, 360),
            Size = new Size(1095, 300),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblPrevTitulo = new Label { Text = "Vista previa del comprobante emitido:", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(15, 12), AutoSize = true };
        _lblRutaArchivo = new Label { Text = "Archivo: (Generado en memoria)", Font = new Font("Segoe UI", 9.5f, FontStyle.Italic), ForeColor = Color.FromArgb(22, 163, 74), Location = new Point(350, 12), AutoSize = true };

        _txtVistaPrevia = new TextBox
        {
            Location = new Point(15, 40),
            Size = new Size(1060, 240),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10),
            ReadOnly = true,
            BackColor = Color.FromArgb(250, 250, 250)
        };

        panelPreview.Controls.AddRange(new Control[] { lblPrevTitulo, _lblRutaArchivo, _txtVistaPrevia });

        Controls.AddRange(new Control[] {
            lblTitulo, lblSubtitulo, lDesde, _dtpDesde, lHasta, _dtpHasta, btnConsultar,
            panelKpis, lSelPed, _cbPedidos, btnVistaPreviaVenta, btnTxtVenta, btnTxtDevolucion, panelPreview
        });
    }

    private static Panel CrearKpi(string titulo, out Label valorLabel, int posX)
    {
        var panel = new Panel
        {
            Location = new Point(posX, 0),
            Size = new Size(345, 75),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lbl = new Label { Text = titulo, Location = new Point(15, 10), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 9.5f) };
        valorLabel = new Label { Text = "0", Location = new Point(12, 32), AutoSize = true, ForeColor = Color.FromArgb(15, 23, 42), Font = new Font("Segoe UI", 18, FontStyle.Bold) };

        panel.Controls.AddRange(new Control[] { lbl, valorLabel });
        return panel;
    }

    public async Task RecargarAsync()
    {
        var pedidos = await _consultas.PedidosAsync();
        var devoluciones = await _consultas.DevolucionesAsync();

        var desde = _dtpDesde.Value.Date;
        var hasta = _dtpHasta.Value.Date.AddDays(1).AddTicks(-1);

        var pedidosRango = pedidos.Where(p => p.FechaCreacion >= desde && p.FechaCreacion <= hasta).ToList();
        var confirmados = pedidosRango.Count(p => p.Estado == EstadoPedido.PAGADO || p.Estado == EstadoPedido.DESPACHADO || p.Estado == EstadoPedido.ENTREGADO);
        var ventas = pedidosRango.Where(p => p.Estado != EstadoPedido.CANCELADO && p.Estado != EstadoPedido.BORRADOR).Sum(p => p.Total);
        var devs = devoluciones.Count(d => d.FechaSolicitud >= desde && d.FechaSolicitud <= hasta);

        _lblPedidosConfirmados.Text = confirmados.ToString("D2");
        _lblVentasNetas.Text = $"USD {ventas:N2}";
        _lblDevoluciones.Text = devs.ToString("D2");

        _cbPedidos.Items.Clear();
        foreach (var p in pedidos)
        {
            _cbPedidos.Items.Add($"{p.Id} | {p.Codigo} | {p.Cliente?.Nombre} | USD {p.Total:F2} | {p.Estado}");
        }
        if (_cbPedidos.Items.Count > 0) _cbPedidos.SelectedIndex = 0;
    }

    private async Task EmitirComprobanteAsync(bool esDevolucion, bool exportarTxt)
    {
        _errores.Clear();
        if (_cbPedidos.SelectedItem == null)
        {
            ValidacionCampo.Mostrar(_errores, _cbPedidos, "Seleccione un pedido para generar el comprobante.");
            return;
        }

        var idStr = _cbPedidos.SelectedItem.ToString()!.Split('|')[0].Trim();
        if (!int.TryParse(idStr, out var id)) return;

        var pedido = await _consultas.PedidoPorIdAsync(id);
        if (pedido == null) return;

        var devolucion = esDevolucion
            ? await _consultas.DevolucionPorPedidoAsync(pedido.Id)
            : null;
        if (esDevolucion && devolucion == null)
        {
            ValidacionCampo.Mostrar(_errores, _cbPedidos, "Este pedido todavía no tiene una devolución registrada.");
            return;
        }

        var resultado = _emisor.Emitir(pedido, devolucion, exportarTxt);

        _txtVistaPrevia.Text = resultado.Contenido;
        if (resultado.RutaArchivo != null)
        {
            _lblRutaArchivo.Text = $"Archivo TXT generado: {resultado.RutaArchivo}";
            MessageBox.Show($"Comprobante exportado exitosamente a archivo TXT real:\n{resultado.RutaArchivo}", "Bridge Archivo TXT", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            _lblRutaArchivo.Text = "Modo: Vista Previa en Pantalla (ISalidaDocumento: VistaPreviaSalida)";
        }
    }
}
