namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Ports;
using LogiMarket.Application.Ventas;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class ClientesView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly ErrorProvider _errores = new();
    private readonly GestionVentas _ventas;
    private readonly IFormularioPrincipalMediator _mediator;

    private TextBox _txtBuscar = null!;
    private DataGridView _gridClientes = null!;
    private TextBox _txtNombre = null!;
    private ComboBox _cbCategoria = null!;
    private TextBox _txtContacto = null!;
    private Cliente? _clienteSeleccionado;

    public ClientesView(ConsultasOperativas consultas, GestionVentas ventas, IFormularioPrincipalMediator mediator)
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

        var lblTitulo = new Label { Text = "Gestión de clientes", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Registro, búsqueda y categorización comercial", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        var lblBuscar = new Label { Text = "Buscar cliente:", Location = new Point(25, 90), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        _txtBuscar = new TextBox { Location = new Point(25, 115), Size = new Size(350, 30), Font = new Font("Segoe UI", 10), PlaceholderText = "Nombre o identificación..." };
        _txtBuscar.TextChanged += async (s, e) => await FiltrarAsync();

        var btnNuevo = new Button { Text = "Nuevo cliente", Location = new Point(390, 114), Size = new Size(130, 32), BackColor = Color.FromArgb(44, 62, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        btnNuevo.Click += async (s, e) => await CrearNuevoClienteAsync();

        _gridClientes = new DataGridView
        {
            Location = new Point(25, 160),
            Size = new Size(680, 480),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        _gridClientes.Columns.Add("Codigo", "Código");
        _gridClientes.Columns.Add("Nombre", "Nombre");
        _gridClientes.Columns.Add("Categoria", "Categoría");
        _gridClientes.Columns.Add("Contacto", "Contacto");
        _gridClientes.SelectionChanged += OnSeleccionCliente;

        // Ficha lateral del cliente
        var panelFicha = new Panel
        {
            Location = new Point(720, 115),
            Size = new Size(410, 400),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblFicha = new Label { Text = "Ficha del cliente", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 15), AutoSize = true };

        var lblNom = new Label { Text = "Nombre:", Location = new Point(20, 60), AutoSize = true };
        _txtNombre = new TextBox { Location = new Point(20, 85), Size = new Size(365, 30), Font = new Font("Segoe UI", 10) };

        var lblCat = new Label { Text = "Categoría:", Location = new Point(20, 130), AutoSize = true };
        _cbCategoria = new ComboBox { Location = new Point(20, 155), Size = new Size(365, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10) };
        _cbCategoria.Items.AddRange(new object[] { CategoriaCliente.NORMAL, CategoriaCliente.VIP, CategoriaCliente.MAYORISTA });

        var lblCont = new Label { Text = "Contacto:", Location = new Point(20, 200), AutoSize = true };
        _txtContacto = new TextBox { Location = new Point(20, 225), Size = new Size(365, 30), Font = new Font("Segoe UI", 10) };

        var btnGuardar = new Button
        {
            Text = "Guardar cambios",
            Location = new Point(20, 290),
            Size = new Size(170, 38),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnGuardar.Click += async (s, e) => await GuardarCambiosAsync();

        var btnCancelar = new Button
        {
            Text = "Cancelar",
            Location = new Point(200, 290),
            Size = new Size(120, 38),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f)
        };
        btnCancelar.Click += (s, e) => LimpiarFormulario();

        panelFicha.Controls.AddRange(new Control[] {
            lblFicha, lblNom, _txtNombre, lblCat, _cbCategoria, lblCont, _txtContacto, btnGuardar, btnCancelar
        });

        Controls.AddRange(new Control[] {
            lblTitulo, lblSubtitulo, lblBuscar, _txtBuscar, btnNuevo, _gridClientes, panelFicha
        });
        AccesibilidadVista.Reacomodar(this, _gridClientes, panelFicha);
    }

    public async Task RecargarAsync()
    {
        var clientes = await _consultas.ClientesAsync();
        _gridClientes.Rows.Clear();
        foreach (var c in clientes)
        {
            _gridClientes.Rows.Add(c.Codigo, c.Nombre, c.Categoria.ToString(), c.Contacto);
        }
    }

    private async Task FiltrarAsync()
    {
        var texto = _txtBuscar.Text.Trim();
        var clientes = await _consultas.BuscarClientesAsync(texto);
        _gridClientes.Rows.Clear();
        foreach (var c in clientes)
        {
            _gridClientes.Rows.Add(c.Codigo, c.Nombre, c.Categoria.ToString(), c.Contacto);
        }
    }

    private async void OnSeleccionCliente(object? sender, EventArgs e)
    {
        if (_gridClientes.CurrentRow == null) return;
        var codigo = _gridClientes.CurrentRow.Cells["Codigo"].Value?.ToString();
        if (string.IsNullOrEmpty(codigo)) return;

        _clienteSeleccionado = await _consultas.ClientePorCodigoAsync(codigo);
        if (_clienteSeleccionado != null)
        {
            _txtNombre.Text = _clienteSeleccionado.Nombre;
            _cbCategoria.SelectedItem = _clienteSeleccionado.Categoria;
            _txtContacto.Text = _clienteSeleccionado.Contacto;
        }
    }

    private async Task GuardarCambiosAsync()
    {
        _errores.Clear();
        if (_clienteSeleccionado == null)
        {
            ValidacionCampo.Mostrar(_errores, _gridClientes, "Seleccione un cliente para editar.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtNombre.Text))
        {
            ValidacionCampo.Mostrar(_errores, _txtNombre, "Ingrese el nombre.");
            return;
        }

        if (_cbCategoria.SelectedItem is not CategoriaCliente categoria)
        {
            ValidacionCampo.Mostrar(_errores, _cbCategoria, "Seleccione una categoría.");
            return;
        }
        try
        {
            await _ventas.ActualizarClienteAsync(_clienteSeleccionado.Id, _txtNombre.Text, categoria, _txtContacto.Text);
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Clientes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        MessageBox.Show("Cliente actualizado correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RecargarAsync();
    }

    private async Task CrearNuevoClienteAsync()
    {
        var codigo = $"C{Random.Shared.Next(100, 999)}";
        var nuevo = new Cliente(codigo, "Nuevo Cliente Demo", CategoriaCliente.NORMAL, "cliente@ejemplo.test");
        try
        {
            await _ventas.AgregarClienteAsync(nuevo);
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Clientes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await RecargarAsync();
    }

    private void LimpiarFormulario()
    {
        _clienteSeleccionado = null;
        _txtNombre.Clear();
        _txtContacto.Clear();
        if (_cbCategoria.Items.Count > 0) _cbCategoria.SelectedIndex = 0;
    }
}
