namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Ventas;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Observer;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class DevolucionesView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly ErrorProvider _errores = new();
    private readonly OperacionesPedido _operaciones;
    private readonly IFormularioPrincipalMediator _mediator;

    private ComboBox _cbPedidosEntregados = null!;
    private ComboBox _cbMotivo = null!;
    private CheckBox _chkSinUso = null!;
    private TextBox _txtObservaciones = null!;
    private Label _lblFechaEntrega = null!;
    private Label _lblReembolsoEstimado = null!;
    private Button _btnConfirmarDevolucion = null!;
    private Pedido? _pedidoSeleccionado;

    public DevolucionesView(ConsultasOperativas consultas, OperacionesPedido operaciones,
        IFormularioPrincipalMediator mediator)
    {
        _operaciones = operaciones;
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

        var lblTitulo = new Label { Text = "Devoluciones y reintegros", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Solicitud total, política de motivo y reembolso único (sin costo de envío)", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        var lPed = new Label { Text = "Pedido entregado:", Location = new Point(25, 95), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _cbPedidosEntregados = new ComboBox { Location = new Point(25, 120), Size = new Size(350, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10) };
        _cbPedidosEntregados.SelectedIndexChanged += async (s, e) => await OnPedidoSeleccionadoAsync();

        var lMot = new Label { Text = "Motivo de devolución:", Location = new Point(395, 95), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _cbMotivo = new ComboBox { Location = new Point(395, 120), Size = new Size(250, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10) };
        _cbMotivo.Items.AddRange(new object[] { MotivoDevolucion.DEFECTO, MotivoDevolucion.ARREPENTIMIENTO });
        _cbMotivo.SelectedIndex = 0;
        _cbMotivo.SelectedIndexChanged += (s, e) => ActualizarReembolso();

        // Card 1: Validación de Política
        var cardPolitica = new Panel
        {
            Location = new Point(25, 175),
            Size = new Size(550, 380),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblPolTitulo = new Label { Text = "Validación de política (Template Method)", Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 20), AutoSize = true };

        _lblFechaEntrega = new Label { Text = "✓ Entrega registrada: [Seleccione pedido]", Location = new Point(20, 65), AutoSize = true, Font = new Font("Segoe UI", 10) };
        var lDef = new Label { Text = "✓ DEFECTO: hasta 30 días posteriores a la entrega", Location = new Point(20, 100), AutoSize = true, Font = new Font("Segoe UI", 10) };
        var lArr = new Label { Text = "✓ ARREPENTIMIENTO: hasta 7 días y producto sin uso", Location = new Point(20, 135), AutoSize = true, Font = new Font("Segoe UI", 10) };
        var lRei = new Label { Text = "✓ Reintegrar existencias: SKU finales una sola vez", Location = new Point(20, 170), AutoSize = true, Font = new Font("Segoe UI", 10) };

        _chkSinUso = new CheckBox { Text = "Producto en condición intacta / sin uso", Location = new Point(20, 210), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), Checked = true };

        var lObs = new Label { Text = "Observaciones de la solicitud:", Location = new Point(20, 250), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
        _txtObservaciones = new TextBox { Location = new Point(20, 275), Size = new Size(505, 75), Multiline = true, Font = new Font("Segoe UI", 9.5f), Text = "Solicitud de devolución registrada en formulario." };

        cardPolitica.Controls.AddRange(new Control[] {
            lblPolTitulo, _lblFechaEntrega, lDef, lArr, lRei, _chkSinUso, lObs, _txtObservaciones
        });

        // Card 2: Resumen del Reembolso
        var cardResumen = new Panel
        {
            Location = new Point(600, 175),
            Size = new Size(520, 380),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblResumen = new Label { Text = "Resumen financiero", Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 20), AutoSize = true };

        var lAclaracion = new Label
        {
            Text = "Nota: La tarifa de despacho original NO es reembolsable.\nSolo se restituye el importe neto de los artículos adquiridos.",
            Location = new Point(20, 65),
            Size = new Size(475, 45),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 9.5f)
        };

        var lTot = new Label { Text = "Total del reembolso simulado:", Location = new Point(20, 140), AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
        _lblReembolsoEstimado = new Label { Text = "USD 0.00", Location = new Point(20, 175), AutoSize = true, Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };

        _btnConfirmarDevolucion = new Button
        {
            Text = "Confirmar devolución",
            Location = new Point(20, 280),
            Size = new Size(475, 45),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnConfirmarDevolucion.Click += async (s, e) => await ProcesarDevolucionAsync();

        cardResumen.Controls.AddRange(new Control[] {
            lblResumen, lAclaracion, lTot, _lblReembolsoEstimado, _btnConfirmarDevolucion
        });

        Controls.AddRange(new Control[] {
            lblTitulo, lblSubtitulo, lPed, _cbPedidosEntregados, lMot, _cbMotivo, cardPolitica, cardResumen
        });
        AccesibilidadVista.Reacomodar(this, cardPolitica, cardResumen);
    }

    public async Task RecargarAsync()
    {
        var pedidos = await _consultas.PedidosAsync();
        var entregadosSinDevolucion = pedidos
            .Where(p => p.Estado == EstadoPedido.ENTREGADO && (p.Devolucion == null || p.Devolucion.Estado != "APROBADA"))
            .ToList();

        _cbPedidosEntregados.Items.Clear();
        foreach (var p in entregadosSinDevolucion)
        {
            _cbPedidosEntregados.Items.Add($"{p.Id} | {p.Codigo} | {p.Cliente?.Nombre} | Total: USD {p.Total:F2}");
        }

        if (_cbPedidosEntregados.Items.Count > 0)
        {
            _cbPedidosEntregados.SelectedIndex = 0;
            await OnPedidoSeleccionadoAsync();
        }
        else
        {
            _lblFechaEntrega.Text = "No hay pedidos ENTREGADOS disponibles para devolución.";
            _lblReembolsoEstimado.Text = "USD 0.00";
            _pedidoSeleccionado = null;
        }
    }

    private async Task OnPedidoSeleccionadoAsync()
    {
        if (_cbPedidosEntregados.SelectedItem == null) return;
        var texto = _cbPedidosEntregados.SelectedItem.ToString() ?? "";
        var idStr = texto.Split('|')[0].Trim();
        if (int.TryParse(idStr, out var id))
        {
            _pedidoSeleccionado = await _consultas.PedidoPorIdAsync(id);
            if (_pedidoSeleccionado != null)
            {
                var fechaEnt = _pedidoSeleccionado.FechaEntrega ?? DateTime.UtcNow;
                _lblFechaEntrega.Text = $"✓ Entrega registrada: {fechaEnt:dd/MM/yyyy HH:mm}";
                ActualizarReembolso();
            }
        }
    }

    private void ActualizarReembolso()
    {
        if (_pedidoSeleccionado == null) return;
        // Reembolso = Subtotal − Descuento (sin incluir TarifaEnvio)
        var reembolso = _operaciones.EstimarReembolso(_pedidoSeleccionado);
        _lblReembolsoEstimado.Text = $"USD {reembolso:F2}";
    }

    private async Task ProcesarDevolucionAsync()
    {
        _errores.Clear();
        if (_pedidoSeleccionado == null)
        {
            ValidacionCampo.Mostrar(_errores, _cbPedidosEntregados, "Seleccione un pedido entregado.");
            return;
        }

        if (_cbMotivo.SelectedItem is not MotivoDevolucion motivo)
        {
            ValidacionCampo.Mostrar(_errores, _cbMotivo, "Seleccione el motivo de la devolución.");
            return;
        }

        if (MessageBox.Show(this, $"¿Procesar la devolución de {_pedidoSeleccionado.Codigo}?",
                "Confirmar devolución", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
            != DialogResult.Yes) return;

        var res = await _operaciones.DevolverAsync(new SolicitarDevolucionDto(
            _pedidoSeleccionado.Id, motivo, _chkSinUso.Checked, _txtObservaciones.Text.Trim()));
        if (res.Exitoso)
        {
            MessageBox.Show(res.Mensaje, "Devolución Aprobada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await _mediator.NotificarOperacionAsync("DEVOLUCION_PROCESADA", _pedidoSeleccionado.Id);
            await RecargarAsync();
        }
        else
        {
            MessageBox.Show(res.Mensaje, "Devolución Rechazada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
