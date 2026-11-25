namespace LogiMarket.WinForms;

using LogiMarket.Application.Ports;
using LogiMarket.WinForms.Views;

public class MainForm : Form, IVistaRecargable
{
    private readonly IServicioSesion _servicioSesion;
    private readonly IFormularioPrincipalMediator _mediator;

    private readonly AccesoView _accesoView;
    private readonly DashboardView _dashboardView;
    private readonly CatalogoKitsView _catalogoKitsView;
    private readonly ClientesView _clientesView;
    private readonly CarritoBorradorView _carritoBorradorView;
    private readonly PedidosHistorialView _pedidosHistorialView;
    private readonly CobroView _cobroView;
    private readonly DespachoView _despachoView;
    private readonly DevolucionesView _devolucionesView;
    private readonly InventarioView _inventarioView;
    private readonly ReportesView _reportesView;
    private readonly AuditoriaPruebasView _auditoriaPruebasView;

    private Panel _panelTop = null!;
    private Panel _panelSidebar = null!;
    private Panel _panelContenido = null!;
    private Label _lblSesionRol = null!;
    private readonly Dictionary<string, (Button Boton, UserControl Vista)> _navegacion = new();

    public MainForm(
        IServicioSesion servicioSesion,
        IFormularioPrincipalMediator mediator,
        AccesoView accesoView,
        DashboardView dashboardView,
        CatalogoKitsView catalogoKitsView,
        ClientesView clientesView,
        CarritoBorradorView carritoBorradorView,
        PedidosHistorialView pedidosHistorialView,
        CobroView cobroView,
        DespachoView despachoView,
        DevolucionesView devolucionesView,
        InventarioView inventarioView,
        ReportesView reportesView,
        AuditoriaPruebasView auditoriaPruebasView)
    {
        _servicioSesion = servicioSesion;
        _mediator = mediator;
        _accesoView = accesoView;
        _dashboardView = dashboardView;
        _catalogoKitsView = catalogoKitsView;
        _clientesView = clientesView;
        _carritoBorradorView = carritoBorradorView;
        _pedidosHistorialView = pedidosHistorialView;
        _cobroView = cobroView;
        _despachoView = despachoView;
        _devolucionesView = devolucionesView;
        _inventarioView = inventarioView;
        _reportesView = reportesView;
        _auditoriaPruebasView = auditoriaPruebasView;

        InicializarVentana();
        RegistrarEnMediador();
    }

    private void InicializarVentana()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "LogiMarket 360 - Aplicativo Empresarial de Ventas y Logística";
        Size = new Size(1380, 840);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(248, 250, 252);

        // Barra Superior
        _panelTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblLogo = new Label
        {
            Text = "LOGIMARKET 360",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 15),
            AutoSize = true
        };

        var lblControl = new Label
        {
            Text = "Control comercial • Escritorio",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(lblLogo.Left + lblLogo.GetPreferredSize(Size.Empty).Width + 16, 20),
            AutoSize = true
        };

        _lblSesionRol = new Label
        {
            Text = "INVITADO • Iniciar sesión",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Anchor = AnchorStyles.Top,
            Location = new Point(1100, 18),
            AutoSize = true
        };

        _panelTop.Controls.AddRange(new Control[] { lblLogo, lblControl, _lblSesionRol });
        void AlinearSesion()
        {
            var ancho = _lblSesionRol.GetPreferredSize(Size.Empty).Width;
            _lblSesionRol.Left = Math.Max(lblControl.Right + 12, _panelTop.ClientSize.Width - ancho - 20);
        }
        _panelTop.SizeChanged += (_, _) => AlinearSesion();
        _lblSesionRol.TextChanged += (_, _) => AlinearSesion();
        AlinearSesion();

        // Barra Lateral de Navegación (Sidebar)
        _panelSidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 200,
            BackColor = Color.FromArgb(241, 245, 249),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true
        };

        // Panel de Contenido Principal
        _panelContenido = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        ConfigurarBotonesNavegacion();

        Controls.Add(_panelContenido);
        Controls.Add(_panelSidebar);
        Controls.Add(_panelTop);

        Shown += async (s, e) =>
        {
            await NavegarAAsync("Acceso");
            await ActualizarBarraSesionAsync();
        };
    }

    private void ConfigurarBotonesNavegacion()
    {
        var items = new (string Nombre, UserControl Vista)[]
        {
            ("Acceso", _accesoView),
            ("Panel general", _dashboardView),
            ("Catálogo y kits", _catalogoKitsView),
            ("Clientes", _clientesView),
            ("Carrito / borrador", _carritoBorradorView),
            ("Pedidos e historial", _pedidosHistorialView),
            ("Cobro", _cobroView),
            ("Despacho", _despachoView),
            ("Devoluciones", _devolucionesView),
            ("Inventario", _inventarioView),
            ("Reportes", _reportesView),
            ("Auditoría / pruebas", _auditoriaPruebasView)
        };

        int posY = 15;
        foreach (var (nombre, vista) in items)
        {
            var btn = new Button
            {
                Text = nombre,
                Location = new Point(10, posY),
                Size = new Size(178, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(51, 65, 85),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;

            btn.Click += async (s, e) => await NavegarAAsync(nombre);

            _panelSidebar.Controls.Add(btn);
            _navegacion[nombre] = (btn, vista);
            posY += 44;
        }

        var lblFooter = new Label
        {
            Text = "LogiMarket • Escritorio",
            Location = new Point(15, posY + 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(148, 163, 184)
        };
        _panelSidebar.Controls.Add(lblFooter);
    }

    private void RegistrarEnMediador()
    {
        _mediator.RegistrarVista("MainForm", this);
        _mediator.RegistrarVista("Acceso", _accesoView);
        _mediator.RegistrarVista("Dashboard", _dashboardView);
        _mediator.RegistrarVista("Catalogo", _catalogoKitsView);
        _mediator.RegistrarVista("Clientes", _clientesView);
        _mediator.RegistrarVista("Carrito", _carritoBorradorView);
        _mediator.RegistrarVista("Pedidos", _pedidosHistorialView);
        _mediator.RegistrarVista("Cobro", _cobroView);
        _mediator.RegistrarVista("Despacho", _despachoView);
        _mediator.RegistrarVista("Devoluciones", _devolucionesView);
        _mediator.RegistrarVista("Inventario", _inventarioView);
        _mediator.RegistrarVista("Reportes", _reportesView);
        _mediator.RegistrarVista("Auditoria", _auditoriaPruebasView);
    }

    private async Task NavegarAAsync(string nombre)
    {
        if (!_servicioSesion.EstaAutenticado && nombre != "Acceso")
        {
            MessageBox.Show("Inicia sesión para acceder al sistema.", "Acceso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_navegacion.TryGetValue(nombre, out var item)) return;

        // Estilos de botones de navegación
        foreach (var par in _navegacion.Values)
        {
            par.Boton.BackColor = Color.Transparent;
            par.Boton.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            par.Boton.ForeColor = Color.FromArgb(51, 65, 85);
        }

        item.Boton.BackColor = Color.FromArgb(226, 232, 240);
        item.Boton.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        item.Boton.ForeColor = Color.FromArgb(15, 23, 42);

        _panelContenido.Controls.Clear();
        item.Vista.Dock = DockStyle.Fill;
        AccesibilidadVista.Configurar(item.Vista, nombre);
        _panelContenido.Controls.Add(item.Vista);

        if (item.Vista is IVistaRecargable recargable)
        {
            await recargable.RecargarAsync();
        }

        await ActualizarBarraSesionAsync();
    }

    private Task ActualizarBarraSesionAsync()
    {
        if (_servicioSesion.EstaAutenticado)
        {
            var user = _servicioSesion.UsuarioActual!;
            _lblSesionRol.Text = $"{user.Rol} • {user.Username} • Sesión local";
            _lblSesionRol.ForeColor = Color.FromArgb(15, 118, 110);
        }
        else
        {
            _lblSesionRol.Text = "INVITADO • Sesión no iniciada";
            _lblSesionRol.ForeColor = Color.FromArgb(185, 28, 28);
        }
        return Task.CompletedTask;
    }

    public async Task RecargarAsync()
    {
        await ActualizarBarraSesionAsync();
    }
}
