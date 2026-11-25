namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Ports;

public class AuditoriaPruebasView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly ErrorProvider _errores = new();
    private readonly IDemostracionOperativa _demo;
    private readonly IFormularioPrincipalMediator _mediator;

    private DataGridView _gridEventos = null!;
    private CheckBox _chkInducirFalloCobro = null!;
    private CheckBox _chkInducirFalloOutbox = null!;
    private CheckBox _chkNotificarSms = null!;
    private Label _lblFlyweightResultado = null!;

    public AuditoriaPruebasView(
        ConsultasOperativas consultas, IDemostracionOperativa demo,
        IFormularioPrincipalMediator mediator)
    {
        _demo = demo;
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

        var lblTitulo = new Label { Text = "Auditoría y simulación", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Eventos, registros y escenarios controlados de prueba", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        // Card Izquierda: Registro de eventos
        var panelEventos = new Panel
        {
            Location = new Point(25, 100),
            Size = new Size(680, 520),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblEventosTitulo = new Label { Text = "Registro de eventos y trazabilidad", Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(15, 15), AutoSize = true };

        _gridEventos = new DataGridView
        {
            Location = new Point(15, 50),
            Size = new Size(650, 450),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        _gridEventos.Columns.Add("Hora", "Hora");
        _gridEventos.Columns.Add("Evento", "Evento / Acción");
        _gridEventos.Columns.Add("Resultado", "Resultado");

        panelEventos.Controls.AddRange(new Control[] { lblEventosTitulo, _gridEventos });

        // Card Derecha: Modo de demostración y simulación
        var panelDemo = new Panel
        {
            Location = new Point(725, 100),
            Size = new Size(395, 520),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblDemoTitulo = new Label { Text = "Modo de demostración", Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 15), AutoSize = true };

        _chkNotificarSms = new CheckBox { Text = "Enviar avisos simulados por SMS", Location = new Point(20, 60), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
        _chkInducirFalloCobro = new CheckBox { Text = "Inducir rechazo de cobro (Fondos insuficientes)", Location = new Point(20, 100), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
        _chkInducirFalloOutbox = new CheckBox { Text = "Inducir fallo de Outbox (Simular caída notificador)", Location = new Point(20, 140), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };

        var btnAplicarEscenario = new Button
        {
            Text = "Aplicar configuración de simulación",
            Location = new Point(20, 185),
            Size = new Size(355, 38),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAplicarEscenario.Click += (s, e) => AplicarEscenario();

        var btnProcesarOutbox = new Button
        {
            Text = "Procesar eventos Outbox pendientes",
            Location = new Point(20, 235),
            Size = new Size(355, 38),
            BackColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnProcesarOutbox.Click += async (s, e) => await ProcesarOutboxAsync();

        var btnRecuperarFallidos = new Button
        {
            Text = "Reprocesar Outbox fallidos (Recuperación)",
            Location = new Point(20, 285),
            Size = new Size(355, 38),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnRecuperarFallidos.Click += async (s, e) => await RecuperarOutboxFallidosAsync();

        // Demostración Flyweight
        var btnFlyweight = new Button
        {
            Text = "Demostración Flyweight (1.000 renglones)",
            Location = new Point(20, 340),
            Size = new Size(355, 40),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnFlyweight.Click += (s, e) => EjecutarDemoFlyweight();

        _lblFlyweightResultado = new Label
        {
            Text = "Flyweight: Listo para probar (1.000 filas con 5 metadatos compartidos).",
            Location = new Point(20, 395),
            Size = new Size(355, 105),
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(71, 85, 105),
            BorderStyle = BorderStyle.FixedSingle
        };

        panelDemo.Controls.AddRange(new Control[] {
            lblDemoTitulo, _chkNotificarSms, _chkInducirFalloCobro, _chkInducirFalloOutbox,
            btnAplicarEscenario, btnProcesarOutbox, btnRecuperarFallidos, btnFlyweight, _lblFlyweightResultado
        });

        Controls.AddRange(new Control[] { lblTitulo, lblSubtitulo, panelEventos, panelDemo });
    }

    public async Task RecargarAsync()
    {
        var auditorias = await _consultas.AuditoriaAsync();
        var outbox = await _consultas.EventosAsync();

        _gridEventos.Rows.Clear();

        // Combinar eventos de auditoría y outbox cronológicamente
        var eventos = auditorias.Select(a => new
        {
            Hora = a.Fecha.ToString("HH:mm:ss"),
            Evento = $"{a.Accion} ({a.Entidad})",
            Resultado = a.Exitoso ? "APROBADO / OK" : "ERROR / RECHAZADO",
            Fecha = a.Fecha
        }).Concat(outbox.Select(o => new
        {
            Hora = o.FechaOcurrencia.ToString("HH:mm:ss"),
            Evento = $"OUTBOX: {o.TipoEvento}",
            Resultado = o.Estado.ToString(),
            Fecha = o.FechaOcurrencia
        })).OrderByDescending(x => x.Fecha).Take(50);

        foreach (var ev in eventos)
        {
            _gridEventos.Rows.Add(ev.Hora, ev.Evento, ev.Resultado);
        }
    }

    private void AplicarEscenario()
    {
        if (!_demo.Configurar(_chkInducirFalloCobro.Checked, _chkInducirFalloOutbox.Checked, _chkNotificarSms.Checked))
        {
            ValidacionCampo.Mostrar(_errores, _chkInducirFalloCobro, "Solo ADMIN puede configurar la simulación.");
            return;
        }

        MessageBox.Show(
            $"Configuración de simulación aplicada:\n" +
            $"• Rechazo de cobro: {(_chkInducirFalloCobro.Checked ? "ACTIVADO" : "DESACTIVADO")}\n" +
            $"• Fallo de Outbox: {(_chkInducirFalloOutbox.Checked ? "ACTIVADO" : "DESACTIVADO")}\n" +
            $"• Avisos por SMS: {(_chkNotificarSms.Checked ? "ACTIVADO" : "DESACTIVADO")}",
            "Simulación Actualizada",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async Task ProcesarOutboxAsync()
    {
        _errores.Clear();
        var cantidad = await _demo.ProcesarEventosAsync(false);
        if (cantidad < 0)
        {
            ValidacionCampo.Mostrar(_errores, _chkInducirFalloOutbox, "Solo ADMIN puede procesar eventos de prueba.");
            return;
        }
        MessageBox.Show($"Procesamiento de Outbox completado.\nEventos procesados: {cantidad}", "Outbox Processor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RecargarAsync();
        await _mediator.NotificarOperacionAsync("EVENTO_OUTBOX");
    }

    private async Task RecuperarOutboxFallidosAsync()
    {
        var cantidad = await _demo.ProcesarEventosAsync(true);
        if (cantidad < 0)
        {
            ValidacionCampo.Mostrar(_errores, _chkInducirFalloOutbox, "Solo ADMIN puede procesar eventos de prueba.");
            return;
        }
        MessageBox.Show($"Recuperación y reintento completado.\nEventos reprocesados exitosamente: {cantidad}", "Recuperación Outbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RecargarAsync();
        await _mediator.NotificarOperacionAsync("EVENTO_OUTBOX");
    }

    private void EjecutarDemoFlyweight()
    {
        var resultado = _demo.MedirCatalogo();
        _lblFlyweightResultado.Text =
            $"Filas generadas: {resultado.Filas:N0}\n" +
            $"Instancias compartidas: {resultado.InstanciasCompartidas}\n" +
            $"Instancias si se crea un objeto por fila: {resultado.InstanciasSinCompartir:N0}\n" +
            $"Filas 1 y 6 comparten referencia: {resultado.MismaReferencia}";

        MessageBox.Show(_lblFlyweightResultado.Text, "Patrón Flyweight Validado", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
