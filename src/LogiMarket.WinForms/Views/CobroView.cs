namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Command;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Facade;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class CobroView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly ErrorProvider _errores = new();
    private readonly ConfirmacionPedidoFacade _facade;
    private readonly CommandInvoker _invocador;
    private readonly IServicioSesion _servicioSesion;
    private readonly IFormularioPrincipalMediator _mediator;

    private ComboBox _cbPedidosPorCobrar = null!;
    private Label _lblPedidoTitulo = null!;
    private Label _lblCliente = null!;
    private Label _lblEstado = null!;
    private Label _lblSubtotal = null!;
    private Label _lblDescuento = null!;
    private Label _lblEntrega = null!;
    private Label _lblTotal = null!;
    private ComboBox _cbProveedor = null!;
    private Label _lblRolBadge = null!;
    private Label _lblResultadoCobro = null!;
    private Button _btnConfirmarCobro = null!;
    private Pedido? _pedidoSeleccionado;

    public CobroView(
        ConsultasOperativas consultas, ConfirmacionPedidoFacade facade,
        CommandInvoker invocador,
        IServicioSesion servicioSesion,
        IFormularioPrincipalMediator mediator)
    {
        _facade = facade;
        _invocador = invocador;
        _servicioSesion = servicioSesion;
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

        var lblTitulo = new Label { Text = "Confirmación y cobro", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Autorización del rol, validaciones y pasarela simulada", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        var lblSel = new Label { Text = "Seleccionar pedido por cobrar:", Location = new Point(25, 90), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _cbPedidosPorCobrar = new ComboBox { Location = new Point(25, 115), Size = new Size(500, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10) };
        _cbPedidosPorCobrar.SelectedIndexChanged += async (s, e) => await OnPedidoSeleccionadoAsync();

        // Card Izquierda: Detalle del Pedido
        var cardPedido = new Panel
        {
            Location = new Point(25, 160),
            Size = new Size(480, 420),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblPedidoTitulo = new Label { Text = "Pedido [Seleccione uno]", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 20), AutoSize = true };

        var lCli = new Label { Text = "Cliente:", Location = new Point(20, 70), AutoSize = true, Font = new Font("Segoe UI", 10) };
        _lblCliente = new Label { Text = "—", Location = new Point(250, 70), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        var lEst = new Label { Text = "Estado:", Location = new Point(20, 110), AutoSize = true, Font = new Font("Segoe UI", 10) };
        _lblEstado = new Label { Text = "—", Location = new Point(250, 110), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        var lSub = new Label { Text = "Subtotal:", Location = new Point(20, 150), AutoSize = true, Font = new Font("Segoe UI", 10) };
        _lblSubtotal = new Label { Text = "USD 0.00", Location = new Point(250, 150), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        var lDesc = new Label { Text = "Descuento:", Location = new Point(20, 190), AutoSize = true, Font = new Font("Segoe UI", 10) };
        _lblDescuento = new Label { Text = "USD 0.00", Location = new Point(250, 190), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        var lEnt = new Label { Text = "Entrega:", Location = new Point(20, 230), AutoSize = true, Font = new Font("Segoe UI", 10) };
        _lblEntrega = new Label { Text = "USD 0.00", Location = new Point(250, 230), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        var lTot = new Label { Text = "Total a cobrar:", Location = new Point(20, 280), AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
        _lblTotal = new Label { Text = "USD 0.00", Location = new Point(250, 280), AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };

        cardPedido.Controls.AddRange(new Control[] {
            _lblPedidoTitulo, lCli, _lblCliente, lEst, _lblEstado, lSub, _lblSubtotal, lDesc, _lblDescuento, lEnt, _lblEntrega, lTot, _lblTotal
        });

        // Card Derecha: Proceso de Pago
        var cardPago = new Panel
        {
            Location = new Point(530, 160),
            Size = new Size(520, 420),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblPagoTitulo = new Label { Text = "Proceso de pago", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 20), AutoSize = true };

        var lblProv = new Label { Text = "Proveedor simulado:", Location = new Point(20, 70), AutoSize = true, Font = new Font("Segoe UI", 10) };
        _cbProveedor = new ComboBox { Location = new Point(20, 95), Size = new Size(475, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10) };
        _cbProveedor.Items.Add("Pasarela Moderna Simulada");
        _cbProveedor.Items.Add("Pasarela Legada Adaptada (CobrarCentavos)");
        _cbProveedor.SelectedIndex = 0;

        // Badges visuales
        _lblRolBadge = new Label
        {
            Text = "ROL: ADMIN",
            Location = new Point(20, 145),
            Size = new Size(160, 28),
            BackColor = Color.FromArgb(241, 245, 249),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblIdemBadge = new Label
        {
            Text = "COBRO ÚNICO (IDEMPOTENTE)",
            Location = new Point(190, 145),
            Size = new Size(240, 28),
            BackColor = Color.FromArgb(241, 245, 249),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle
        };

        _btnConfirmarCobro = new Button
        {
            Text = "Confirmar cobro",
            Location = new Point(20, 200),
            Size = new Size(475, 45),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnConfirmarCobro.Click += async (s, e) => await EjecutarCobroAsync();

        var lblRes = new Label { Text = "Resultado / referencia:", Location = new Point(20, 270), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
        _lblResultadoCobro = new Label
        {
            Text = "Pendiente de confirmación",
            Location = new Point(20, 295),
            Size = new Size(475, 95),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            BorderStyle = BorderStyle.FixedSingle
        };

        cardPago.Controls.AddRange(new Control[] {
            lblPagoTitulo, lblProv, _cbProveedor, _lblRolBadge, lblIdemBadge, _btnConfirmarCobro, lblRes, _lblResultadoCobro
        });

        Controls.AddRange(new Control[] { lblTitulo, lblSubtitulo, lblSel, _cbPedidosPorCobrar, cardPedido, cardPago });
        AccesibilidadVista.Reacomodar(this, cardPedido, cardPago);
    }

    public async Task RecargarAsync()
    {
        var pedidos = await _consultas.PedidosAsync();
        var pendientes = pedidos.Where(p => p.Estado == EstadoPedido.VALIDADO || p.Estado == EstadoPedido.BORRADOR).ToList();

        _cbPedidosPorCobrar.Items.Clear();
        foreach (var p in pendientes)
        {
            _cbPedidosPorCobrar.Items.Add($"{p.Id} | {p.Codigo} | {p.Cliente?.Nombre} | USD {p.Total:F2} | {p.Estado}");
        }

        if (_cbPedidosPorCobrar.Items.Count > 0)
        {
            _cbPedidosPorCobrar.SelectedIndex = 0;
        }
        else
        {
            LimpiarDetalle();
        }

        var rol = _servicioSesion.RolActual?.ToString() ?? "SIN_SESIÓN";
        _lblRolBadge.Text = $"ROL: {rol}";
        if (rol == "CONSULTA" || rol == "BODEGA")
        {
            _lblRolBadge.BackColor = Color.FromArgb(254, 226, 226); // Rojo tenue
            _lblRolBadge.ForeColor = Color.FromArgb(185, 28, 28);
        }
        else
        {
            _lblRolBadge.BackColor = Color.FromArgb(241, 245, 249);
            _lblRolBadge.ForeColor = Color.Black;
        }
    }

    private async Task OnPedidoSeleccionadoAsync()
    {
        if (_cbPedidosPorCobrar.SelectedItem == null) return;
        var texto = _cbPedidosPorCobrar.SelectedItem.ToString() ?? "";
        var idStr = texto.Split('|')[0].Trim();
        if (int.TryParse(idStr, out var id))
        {
            _pedidoSeleccionado = await _consultas.PedidoPorIdAsync(id);
            if (_pedidoSeleccionado != null)
            {
                _lblPedidoTitulo.Text = $"Pedido {_pedidoSeleccionado.Codigo}";
                _lblCliente.Text = _pedidoSeleccionado.Cliente?.Nombre ?? "N/A";
                _lblEstado.Text = _pedidoSeleccionado.Estado.ToString();
                _lblSubtotal.Text = $"USD {_pedidoSeleccionado.Subtotal:F2}";
                _lblDescuento.Text = $"USD {_pedidoSeleccionado.Descuento:F2}";
                _lblEntrega.Text = $"USD {_pedidoSeleccionado.TarifaEnvio:F2}";
                _lblTotal.Text = $"USD {_pedidoSeleccionado.Total:F2}";
                _lblResultadoCobro.Text = "Listo para procesar confirmación.";
                _lblResultadoCobro.ForeColor = Color.FromArgb(71, 85, 105);
            }
        }
    }

    private void LimpiarDetalle()
    {
        _pedidoSeleccionado = null;
        _lblPedidoTitulo.Text = "Pedido [Sin pendientes]";
        _lblCliente.Text = "—";
        _lblEstado.Text = "—";
        _lblSubtotal.Text = "USD 0.00";
        _lblDescuento.Text = "USD 0.00";
        _lblEntrega.Text = "USD 0.00";
        _lblTotal.Text = "USD 0.00";
        _lblResultadoCobro.Text = "No hay pedidos pendientes de cobro.";
    }

    private async Task EjecutarCobroAsync()
    {
        _errores.Clear();
        if (_pedidoSeleccionado == null)
        {
            ValidacionCampo.Mostrar(_errores, _cbPedidosPorCobrar, "Seleccione un pedido válido para cobrar.");
            return;
        }

        if (MessageBox.Show(this, $"¿Confirmar el cobro de USD {_pedidoSeleccionado.Total:F2}?",
                "Confirmar cobro", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
            != DialogResult.Yes) return;

        var dto = new ConfirmarPedidoDto(
            _pedidoSeleccionado.Id,
            _pedidoSeleccionado.IdempotencyKey,
            _cbProveedor.SelectedIndex == 1 ? "PasarelaLegada" : "PasarelaModerna");

        try
        {
            var resultado = await _invocador.EjecutarAsync(new ConfirmarPedidoCommand(_facade, dto));
            if (resultado.Exitoso)
            {
                _lblResultadoCobro.Text = $"✓ APROBADO\n{resultado.Mensaje}";
                _lblResultadoCobro.ForeColor = Color.FromArgb(22, 163, 74);
                MessageBox.Show(resultado.Mensaje, "Cobro Exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await _mediator.NotificarOperacionAsync("PEDIDO_CONFIRMADO", _pedidoSeleccionado.Id);
                await RecargarAsync();
            }
            else
            {
                _lblResultadoCobro.Text = $"✕ RECHAZADO\n{resultado.Mensaje}";
                _lblResultadoCobro.ForeColor = Color.FromArgb(220, 38, 38);
                MessageBox.Show(resultado.Mensaje, "Cobro Denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _lblResultadoCobro.Text = $"✕ ERROR\n{ex.Message}";
            _lblResultadoCobro.ForeColor = Color.FromArgb(220, 38, 38);
            MessageBox.Show($"Error durante la confirmación: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
