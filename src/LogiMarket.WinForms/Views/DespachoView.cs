namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Ventas;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Observer;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class DespachoView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly ErrorProvider _errores = new();
    private readonly IFormularioPrincipalMediator _mediator;
    private readonly IGuiaEntrega _guiaEntrega;
    private readonly OperacionesPedido _operaciones;

    private DataGridView _gridDespachos = null!;
    private TextBox _txtPedidoCodigo = null!;
    private TextBox _txtGuiaSimulada = null!;
    private Button _btnMarcarDespachado = null!;
    private Button _btnRegistrarEntrega = null!;
    private Pedido? _pedidoSeleccionado;

    public DespachoView(ConsultasOperativas consultas, OperacionesPedido operaciones,
        IFormularioPrincipalMediator mediator, IGuiaEntrega guiaEntrega)
    {
        _operaciones = operaciones;
        _mediator = mediator;
        _guiaEntrega = guiaEntrega;
        _consultas = consultas;
        ValidacionCampo.Configurar(this, _errores);
        InicializarComponentes();
    }

    private void InicializarComponentes()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 250, 252);
        AutoScroll = true;

        var lblTitulo = new Label { Text = "Despacho y entrega", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Preparación, guía interna y transición autorizada (Rol: BODEGA / ADMIN)", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        _gridDespachos = new DataGridView
        {
            Location = new Point(25, 100),
            Size = new Size(650, 480),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        _gridDespachos.Columns.Add("Pedido", "Pedido");
        _gridDespachos.Columns.Add("Cliente", "Cliente");
        _gridDespachos.Columns.Add("Estado", "Estado");
        _gridDespachos.Columns.Add("Entrega", "Entrega");
        _gridDespachos.SelectionChanged += async (s, e) => await OnSeleccionPedidoAsync();

        // Panel Lateral: Detalle logístico
        var panelDetalle = new Panel
        {
            Location = new Point(700, 100),
            Size = new Size(420, 480),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblDetalleTitulo = new Label { Text = "Detalle logístico", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 20), AutoSize = true };

        var lPed = new Label { Text = "Pedido:", Location = new Point(20, 70), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        _txtPedidoCodigo = new TextBox { Location = new Point(20, 95), Size = new Size(375, 30), ReadOnly = true, Font = new Font("Segoe UI", 10) };

        var lGuia = new Label { Text = "Guía simulada (Abstract Factory):", Location = new Point(20, 145), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        _txtGuiaSimulada = new TextBox { Location = new Point(20, 170), Size = new Size(375, 30), Font = new Font("Segoe UI", 10) };

        _btnMarcarDespachado = new Button
        {
            Text = "Marcar como despachado",
            Location = new Point(20, 230),
            Size = new Size(375, 42),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnMarcarDespachado.Click += async (s, e) => await DespacharAsync();

        _btnRegistrarEntrega = new Button
        {
            Text = "Registrar entrega",
            Location = new Point(20, 285),
            Size = new Size(375, 42),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnRegistrarEntrega.Click += async (s, e) => await EntregarAsync();

        panelDetalle.Controls.AddRange(new Control[] {
            lblDetalleTitulo, lPed, _txtPedidoCodigo, lGuia, _txtGuiaSimulada, _btnMarcarDespachado, _btnRegistrarEntrega
        });

        Controls.AddRange(new Control[] { lblTitulo, lblSubtitulo, _gridDespachos, panelDetalle });
        AccesibilidadVista.Reacomodar(this, _gridDespachos, panelDetalle);
    }

    public async Task RecargarAsync()
    {
        var pedidos = await _consultas.PedidosAsync();
        var logisticos = pedidos.Where(p => p.Estado == EstadoPedido.PAGADO || p.Estado == EstadoPedido.DESPACHADO).ToList();

        _gridDespachos.Rows.Clear();
        foreach (var p in logisticos)
        {
            _gridDespachos.Rows.Add(p.Codigo, p.Cliente?.Nombre ?? "N/A", p.Estado.ToString(), p.TipoEnvio.ToString());
        }

        if (_gridDespachos.Rows.Count > 0)
        {
            _gridDespachos.Rows[0].Selected = true;
            await OnSeleccionPedidoAsync();
        }
        else
        {
            _txtPedidoCodigo.Clear();
            _txtGuiaSimulada.Clear();
        }
    }

    private async Task OnSeleccionPedidoAsync()
    {
        if (_gridDespachos.CurrentRow == null) return;
        var codigo = _gridDespachos.CurrentRow.Cells["Pedido"].Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(codigo)) return;

        _pedidoSeleccionado = await _consultas.PedidoPorCodigoAsync(codigo);
        if (_pedidoSeleccionado != null)
        {
            _txtPedidoCodigo.Text = _pedidoSeleccionado.Codigo;

            // Uso de la Fábrica Abstracta para generar la guía adecuada a la familia
            _txtGuiaSimulada.Text = _pedidoSeleccionado.GuiaDespacho
                ?? _guiaEntrega.Generar(_pedidoSeleccionado.TipoEnvio, _pedidoSeleccionado.Codigo);

            _btnMarcarDespachado.Enabled = _pedidoSeleccionado.Estado == EstadoPedido.PAGADO;
            _btnRegistrarEntrega.Enabled = _pedidoSeleccionado.Estado == EstadoPedido.DESPACHADO;
        }
    }

    private async Task DespacharAsync()
    {
        _errores.Clear();
        if (_pedidoSeleccionado == null)
        {
            ValidacionCampo.Mostrar(_errores, _gridDespachos, "Seleccione un pedido para despachar.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtGuiaSimulada.Text))
        {
            ValidacionCampo.Mostrar(_errores, _txtGuiaSimulada, "Ingrese la guía de despacho.");
            return;
        }

        if (MessageBox.Show(this, $"¿Registrar el despacho de {_pedidoSeleccionado.Codigo}?",
                "Confirmar despacho", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
            != DialogResult.Yes) return;

        var res = await _operaciones.DespacharAsync(
            new DespacharPedidoDto(_pedidoSeleccionado.Id, _txtGuiaSimulada.Text.Trim()));
        if (res.Exitoso)
        {
            MessageBox.Show(res.Mensaje, "Despacho Registrado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await _mediator.NotificarOperacionAsync("DESPACHO_REGISTRADO", _pedidoSeleccionado.Id);
            await RecargarAsync();
        }
        else
        {
            MessageBox.Show(res.Mensaje, "Error de Despacho", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task EntregarAsync()
    {
        _errores.Clear();
        if (_pedidoSeleccionado == null)
        {
            ValidacionCampo.Mostrar(_errores, _gridDespachos, "Seleccione un pedido para entregar.");
            return;
        }
        if (MessageBox.Show(this, $"¿Confirmar la entrega de {_pedidoSeleccionado.Codigo}?",
                "Confirmar entrega", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
            != DialogResult.Yes) return;

        var id = _pedidoSeleccionado.Id;
        try
        {
            var resultado = await _operaciones.EntregarAsync(id);
            MessageBox.Show(resultado.Mensaje, "Entrega");
            if (resultado.Exitoso)
            {
                await _mediator.NotificarOperacionAsync("ENTREGA_REGISTRADA", id);
                await RecargarAsync();
            }
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Entrega", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}