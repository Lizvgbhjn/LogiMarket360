namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Ports;
using LogiMarket.Application.Catalogo;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class InventarioView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly GestionInventario _gestion;
    private readonly ErrorProvider _errores = new();
    private readonly IFormularioPrincipalMediator _mediator;

    private DataGridView _gridInventario = null!;
    private ComboBox _cbSkuAjuste = null!;
    private NumericUpDown _numAjuste = null!;
    private TextBox _txtMotivoAjuste = null!;
    private ListBox _listMovimientos = null!;

    public InventarioView(
        ConsultasOperativas consultas, GestionInventario gestion,
        IFormularioPrincipalMediator mediator)
    {
        _gestion = gestion;
        _mediator = mediator;
        _consultas = consultas;
        _errores.ContainerControl = this;
        _errores.BlinkStyle = ErrorBlinkStyle.NeverBlink;
        Disposed += (_, _) => _errores.Dispose();
        InicializarComponentes();
    }

    private void InicializarComponentes()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 250, 252);
        AutoScroll = true;

        var lblTitulo = new Label { Text = "Inventario y movimientos", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Existencias, referencias y ajustes auditados", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        // Grilla de Stock
        _gridInventario = new DataGridView
        {
            Location = new Point(25, 100),
            Size = new Size(680, 360),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        _gridInventario.Columns.Add("SKU", "SKU");
        _gridInventario.Columns.Add("Producto", "Producto");
        _gridInventario.Columns.Add("Disponible", "Disponible");
        _gridInventario.Columns.Add("Alerta", "Alerta");

        // Panel Lateral: Movimiento nuevo (Ajuste)
        var panelAjuste = new Panel
        {
            Location = new Point(730, 100),
            Size = new Size(390, 360),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblAjusteTitulo = new Label { Text = "Movimiento nuevo", Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 20), AutoSize = true };

        var lSku = new Label { Text = "SKU del producto:", Location = new Point(20, 65), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _cbSkuAjuste = new ComboBox { Location = new Point(20, 90), Size = new Size(345, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };

        var lCant = new Label { Text = "Cantidad (+ Entrada / - Salida):", Location = new Point(20, 135), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _numAjuste = new NumericUpDown { Location = new Point(20, 160), Size = new Size(150, 30), Minimum = -100, Maximum = 100, Value = 1, Font = new Font("Segoe UI", 10) };

        var lMot = new Label { Text = "Motivo del ajuste:", Location = new Point(20, 205), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
        _txtMotivoAjuste = new TextBox { Location = new Point(20, 230), Size = new Size(345, 30), Font = new Font("Segoe UI", 9.5f), Text = "Ajuste manual de existencias" };

        var btnRegistrarAjuste = new Button
        {
            Text = "Registrar ajuste",
            Location = new Point(20, 280),
            Size = new Size(345, 42),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnRegistrarAjuste.Click += async (s, e) => await RegistrarAjusteAsync();

        panelAjuste.Controls.AddRange(new Control[] {
            lblAjusteTitulo, lSku, _cbSkuAjuste, lCant, _numAjuste, lMot, _txtMotivoAjuste, btnRegistrarAjuste
        });

        // Panel Inferior: Movimientos Recientes
        var panelMovimientos = new Panel
        {
            Location = new Point(25, 480),
            Size = new Size(1095, 180),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblMovTitulo = new Label { Text = "Movimientos recientes auditados", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(15, 10), AutoSize = true };

        _listMovimientos = new ListBox
        {
            Location = new Point(15, 35),
            Size = new Size(1060, 130),
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(51, 65, 85)
        };

        panelMovimientos.Controls.AddRange(new Control[] { lblMovTitulo, _listMovimientos });

        Controls.AddRange(new Control[] { lblTitulo, lblSubtitulo, _gridInventario, panelAjuste, panelMovimientos });
    }

    public async Task RecargarAsync()
    {
        var productos = await _consultas.ProductosAsync();
        _gridInventario.Rows.Clear();
        _cbSkuAjuste.Items.Clear();

        foreach (var p in productos)
        {
            var alerta = p.Stock <= 5 ? "BAJO" : "OK";
            _gridInventario.Rows.Add(p.Sku, p.Nombre, p.Stock.ToString(), alerta);
            _cbSkuAjuste.Items.Add($"{p.Sku} - {p.Nombre} (Stock actual: {p.Stock})");
        }

        if (_cbSkuAjuste.Items.Count > 0) _cbSkuAjuste.SelectedIndex = 0;

        var movimientos = await _consultas.MovimientosAsync();
        _listMovimientos.Items.Clear();
        foreach (var m in movimientos.Take(20))
        {
            var signo = m.Cantidad > 0 ? "+" : "";
            _listMovimientos.Items.Add($"{m.Fecha:dd/MM/yyyy HH:mm:ss} | SKU: {m.Sku} | Cant: {signo}{m.Cantidad} (Stock: {m.StockAnterior} → {m.StockPosterior}) | Tipo: {m.TipoMovimiento} | Ref: {m.Referencia}");
        }
    }

    private async Task RegistrarAjusteAsync()
    {
        _errores.Clear();
        if (_cbSkuAjuste.SelectedItem is not string seleccion)
        {
            _errores.SetError(_cbSkuAjuste, "Seleccione un producto.");
            _cbSkuAjuste.Focus();
            return;
        }
        if (_numAjuste.Value == 0)
        {
            _errores.SetError(_numAjuste, "El ajuste debe ser distinto de cero.");
            _numAjuste.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtMotivoAjuste.Text))
        {
            _errores.SetError(_txtMotivoAjuste, "Ingrese el motivo del ajuste.");
            _txtMotivoAjuste.Focus();
            return;
        }
        var sku = seleccion.Split(" - ", 2)[0].Trim();
        try
        {
            await _gestion.AjustarAsync(sku, (int)_numAjuste.Value, _txtMotivoAjuste.Text);
            MessageBox.Show($"Ajuste registrado para {sku}.", "Inventario");
            await _mediator.NotificarOperacionAsync("STOCK_AJUSTADO");
            await RecargarAsync();
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Inventario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}