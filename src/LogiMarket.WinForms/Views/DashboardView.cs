namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Ports;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Enums;

public class DashboardView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly IMetricasSesion _metricas;

    private Label _lblPedidosActivos = null!;
    private Label _lblPorDespachar = null!;
    private Label _lblStockBajo = null!;
    private Label _lblVentas = null!;
    private DataGridView _gridPedidos = null!;
    private ListBox _listAlertas = null!;

    public DashboardView(
        ConsultasOperativas consultas, IMetricasSesion metricas)
    {
        _metricas = metricas;
        _consultas = consultas;
        InicializarComponentes();
    }

    private void InicializarComponentes()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 250, 252);
        AutoScroll = true;

        var lblTitulo = new Label
        {
            Text = "Panel general",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(25, 20),
            AutoSize = true
        };

        var lblSubtitulo = new Label
        {
            Text = "Resumen de operaciones, pedidos y movimientos recientes",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(25, 55),
            AutoSize = true
        };

        // 4 KPI Cards
        var panelKpis = new FlowLayoutPanel
        {
            Location = new Point(25, 90),
            Size = new Size(1100, 110),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var card1 = CrearKpiCard("Pedidos activos", out _lblPedidosActivos);
        var card2 = CrearKpiCard("Por despachar", out _lblPorDespachar);
        var card3 = CrearKpiCard("Stock bajo", out _lblStockBajo);
        var card4 = CrearKpiCard("Ventas (USD)", out _lblVentas);

        panelKpis.Controls.AddRange(new Control[] { card1, card2, card3, card4 });

        // Grilla Pedidos Recientes
        var lblPedidos = new Label
        {
            Text = "Pedidos recientes",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(25, 220),
            AutoSize = true
        };

        _gridPedidos = new DataGridView
        {
            Location = new Point(25, 250),
            Size = new Size(740, 360),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        _gridPedidos.Columns.Add("Codigo", "Pedido");
        _gridPedidos.Columns.Add("Cliente", "Cliente");
        _gridPedidos.Columns.Add("Estado", "Estado");
        _gridPedidos.Columns.Add("Total", "Total");

        // Panel lateral Alertas
        var panelAlertas = new Panel
        {
            Location = new Point(785, 220),
            Size = new Size(340, 390),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblAlertaTitulo = new Label
        {
            Text = "Alertas del sistema",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(15, 15),
            AutoSize = true
        };

        _listAlertas = new ListBox
        {
            Location = new Point(15, 50),
            Size = new Size(310, 320),
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        panelAlertas.Controls.AddRange(new Control[] { lblAlertaTitulo, _listAlertas });

        Controls.AddRange(new Control[] { lblTitulo, lblSubtitulo, panelKpis, lblPedidos, _gridPedidos, panelAlertas });
    }

    private static Panel CrearKpiCard(string titulo, out Label valorLabel)
    {
        var panel = new Panel
        {
            Size = new Size(250, 95),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 20, 0)
        };

        var lbl = new Label
        {
            Text = titulo,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(15, 12),
            AutoSize = true
        };

        valorLabel = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(12, 38),
            AutoSize = true
        };

        panel.Controls.AddRange(new Control[] { lbl, valorLabel });
        return panel;
    }

    public async Task RecargarAsync()
    {
        var pedidos = await _consultas.PedidosAsync();
        var productos = await _consultas.ProductosAsync();
        var outbox = await _consultas.EventosPendientesAsync(100);
        var devoluciones = await _consultas.DevolucionesAsync();

        var activos = pedidos.Count(p => p.Estado == EstadoPedido.BORRADOR || p.Estado == EstadoPedido.VALIDADO || p.Estado == EstadoPedido.PAGADO || p.Estado == EstadoPedido.DESPACHADO);
        var porDespachar = pedidos.Count(p => p.Estado == EstadoPedido.PAGADO);
        var stockBajo = productos.Count(p => p.Stock <= 5);
        var ventasTotal = pedidos.Where(p => p.Estado != EstadoPedido.CANCELADO && p.Estado != EstadoPedido.BORRADOR).Sum(p => p.Total);

        _lblPedidosActivos.Text = activos.ToString("D2");
        _lblPorDespachar.Text = porDespachar.ToString("D2");
        _lblStockBajo.Text = stockBajo.ToString("D2");
        _lblVentas.Text = $"USD {ventasTotal:N2}";

        _gridPedidos.Rows.Clear();
        foreach (var p in pedidos.Take(10))
        {
            _gridPedidos.Rows.Add(p.Codigo, p.Cliente?.Nombre ?? "N/A", p.Estado.ToString(), $"USD {p.Total:F2}");
        }

        _listAlertas.Items.Clear();
        if (stockBajo > 0)
            _listAlertas.Items.Add($"• {stockBajo} producto(s) con stock bajo (<= 5)");
        else
            _listAlertas.Items.Add("• Stock en niveles óptimos");

        if (outbox.Count > 0)
            _listAlertas.Items.Add($"• {outbox.Count} evento(s) Outbox pendientes");
        else
            _listAlertas.Items.Add("• Sin eventos Outbox pendientes");

        var devPend = devoluciones.Count;
        _listAlertas.Items.Add($"• {devPend} devolución(es) registradas");
        _listAlertas.Items.Add($"• {pedidos.Count(p => p.Estado == EstadoPedido.ENTREGADO)} pedido(s) entregados");
    }
}
