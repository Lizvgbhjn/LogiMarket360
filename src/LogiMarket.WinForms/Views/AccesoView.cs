namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class AccesoView : UserControl, IVistaRecargable
{
    private readonly Autenticacion _autenticacion;
    private readonly ErrorProvider _errores = new();
    private readonly IServicioSesion _servicioSesion;
    private readonly IFormularioPrincipalMediator _mediator;

    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private Button _btnIngresar = null!;
    private Button _btnCerrarSesion = null!;
    private Label _lblEstado = null!;

    public AccesoView(Autenticacion autenticacion, IServicioSesion servicioSesion, IFormularioPrincipalMediator mediator)
    {
        _autenticacion = autenticacion;
        _servicioSesion = servicioSesion;
        _mediator = mediator;
        InicializarComponentes();
    }

    private void InicializarComponentes()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 250, 252);

        var panelCard = new Panel
        {
            Size = new Size(520, 480),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.None
        };
        panelCard.Location = new Point(Math.Max(8, (Width - panelCard.Width) / 2), Math.Max(20, (Height - panelCard.Height) / 2));

        var lblTitulo = new Label
        {
            Text = "ACCESO AL SISTEMA",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 45
        };

        var lblSubtitulo = new Label
        {
            Text = "Credenciales de demostración y perfil de acceso",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 25
        };

        var lblUser = new Label { Text = "Usuario:", Location = new Point(50, 90), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Regular) };
        _txtUsername = new TextBox { Location = new Point(50, 115), Size = new Size(420, 32), Font = new Font("Segoe UI", 11), Text = "admin.demo" };

        var lblPass = new Label { Text = "Contraseña:", Location = new Point(50, 160), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Regular) };
        _txtPassword = new TextBox { Location = new Point(50, 185), Size = new Size(420, 32), Font = new Font("Segoe UI", 11), UseSystemPasswordChar = true, Text = "admin123" };

        _btnIngresar = new Button
        {
            Text = "Ingresar",
            Location = new Point(50, 240),
            Size = new Size(420, 42),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnIngresar.Click += async (s, e) => await IngresarAsync();

        _btnCerrarSesion = new Button
        {
            Text = "Cerrar sesión actual",
            Location = new Point(50, 290),
            Size = new Size(420, 35),
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _btnCerrarSesion.Click += async (s, e) => await CerrarSesionAsync();

        _lblEstado = new Label
        {
            Text = "Roles disponibles: ADMIN • VENTAS • BODEGA • CONSULTA",
            Location = new Point(20, 335),
            Size = new Size(480, 25),
            ForeColor = Color.FromArgb(71, 85, 105),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9)
        };

        // Botones de acceso rápido para pruebas
        var lblDemo = new Label { Text = "Acceso rápido para evaluación:", Location = new Point(50, 370), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var btnAdmin = new Button { Text = "ADMIN", Location = new Point(50, 395), Size = new Size(95, 30), BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
        var btnVentas = new Button { Text = "VENTAS", Location = new Point(155, 395), Size = new Size(95, 30), BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
        var btnBodega = new Button { Text = "BODEGA", Location = new Point(260, 395), Size = new Size(95, 30), BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };
        var btnConsulta = new Button { Text = "CONSULTA", Location = new Point(365, 395), Size = new Size(105, 30), BackColor = Color.FromArgb(241, 245, 249), FlatStyle = FlatStyle.Flat };

        btnAdmin.Click += (s, e) => { _txtUsername.Text = "admin.demo"; _txtPassword.Text = "admin123"; };
        btnVentas.Click += (s, e) => { _txtUsername.Text = "ventas.demo"; _txtPassword.Text = "ventas123"; };
        btnBodega.Click += (s, e) => { _txtUsername.Text = "bodega.demo"; _txtPassword.Text = "bodega123"; };
        btnConsulta.Click += (s, e) => { _txtUsername.Text = "consulta.demo"; _txtPassword.Text = "consulta123"; };

        panelCard.Controls.AddRange(new Control[] {
            lblTitulo, lblSubtitulo, lblUser, _txtUsername, lblPass, _txtPassword,
            _btnIngresar, _btnCerrarSesion, _lblEstado, lblDemo, btnAdmin, btnVentas, btnBodega, btnConsulta
        });

        Controls.Add(panelCard);
        AutoScroll = true;
        _errores.ContainerControl = this;
        _errores.BlinkStyle = ErrorBlinkStyle.NeverBlink;
        Disposed += (_, _) => _errores.Dispose();
        _txtUsername.TabIndex = 0;
        _txtUsername.AccessibleName = "Usuario";
        _txtPassword.TabIndex = 1;
        _txtPassword.AccessibleName = "Contraseña";
        _btnIngresar.TabIndex = 2;
        _btnCerrarSesion.TabIndex = 3;
        Resize += (s, e) =>
        {
            panelCard.Location = new Point(Math.Max(8, (Width - panelCard.Width) / 2), Math.Max(20, (Height - panelCard.Height) / 2));
        };
    }

    private async Task IngresarAsync()
    {
        var username = _txtUsername.Text.Trim();
        var pass = _txtPassword.Text;

        _errores.Clear();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(pass))
        {
            var campo = string.IsNullOrWhiteSpace(username) ? _txtUsername : _txtPassword;
            _errores.SetError(campo, "Este campo es obligatorio.");
            campo.Focus();
            return;
        }

        if (!await _autenticacion.IngresarAsync(username, pass))
        {
            _errores.SetError(_txtPassword, "Credenciales inválidas.");
            _txtPassword.Focus();
            return;
        }

        var user = _servicioSesion.UsuarioActual!;
        _lblEstado.Text = $"Sesión iniciada como: {user.Username} ({user.Rol})";
        _btnCerrarSesion.Visible = true;
        await _mediator.NotificarOperacionAsync("LOGIN");
        MessageBox.Show($"Bienvenido {user.Username}.\nRol: {user.Rol}", "Sesión Iniciada", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CerrarSesionAsync()
    {
        _servicioSesion.CerrarSesion();
        _lblEstado.Text = "Sesión cerrada. Ingrese sus credenciales.";
        _btnCerrarSesion.Visible = false;
        await _mediator.NotificarOperacionAsync("LOGOUT");
    }

    public Task RecargarAsync()
    {
        if (_servicioSesion.EstaAutenticado)
        {
            _lblEstado.Text = $"Sesión activa: {_servicioSesion.UsuarioActual?.Username} ({_servicioSesion.RolActual})";
            _btnCerrarSesion.Visible = true;
        }
        else
        {
            _btnCerrarSesion.Visible = false;
            _lblEstado.Text = "Roles disponibles: ADMIN • VENTAS • BODEGA • CONSULTA";
        }
        return Task.CompletedTask;
    }
}
