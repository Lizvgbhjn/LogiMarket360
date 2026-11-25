namespace LogiMarket.WinForms;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Chain;
using LogiMarket.Application.Command;
using LogiMarket.Application.Facade;
using LogiMarket.Application.Observer;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Proxy;
using LogiMarket.Application.Singleton;
using LogiMarket.Application.Ventas;
using LogiMarket.Infrastructure.Adapter;
using LogiMarket.Infrastructure.AbstractFactory;
using LogiMarket.Infrastructure.Bridge;
using LogiMarket.Infrastructure.Demostracion;
using LogiMarket.Infrastructure.Decorator;
using LogiMarket.Infrastructure.FactoryMethod;
using LogiMarket.Infrastructure.Flyweight;
using LogiMarket.Infrastructure.Outbox;
using LogiMarket.Infrastructure.Observer;
using LogiMarket.Infrastructure.Persistence;
using LogiMarket.Infrastructure.Session;
using LogiMarket.WinForms.Views;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();
        ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();

        // 1. Inicializar base de datos y semilla idempotente
        using (var scope = serviceProvider.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            seeder.InicializarAsync().GetAwaiter().GetResult();
        }

        // 2. Ejecutar formulario principal
        using var uiScope = serviceProvider.CreateScope();
        var mainForm = uiScope.ServiceProvider.GetRequiredService<MainForm>();
        using var terminarOutbox = new CancellationTokenSource();
        var outbox = serviceProvider.GetRequiredService<OutboxAutomatico>();
        var trabajoOutbox = Task.Run(() => outbox.EjecutarAsync(TimeSpan.FromSeconds(15), terminarOutbox.Token));
        try
        {
            System.Windows.Forms.Application.Run(mainForm);
        }
        finally
        {
            terminarOutbox.Cancel();
            trabajoOutbox.GetAwaiter().GetResult();
        }
    }

    private static IPasarelaPago ProtegerPasarela(IServiceProvider services, IPasarelaPago pasarela)
    {
        var sesion = services.GetRequiredService<IServicioSesion>();
        var decorador = new AuditoriaPagoDecorator(pasarela, services.GetRequiredService<AuditoriaCobroTransaccional>(), sesion);
        return new PagoAutorizadoProxy(decorador, sesion, services.GetRequiredService<IMetricasSesion>());
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Persistencia EF Core SQLite
        var rutaDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logimarket360-migrada.db");
        services.AddDbContext<LogiMarketDbContext>(options =>
            options.UseSqlite($"Data Source={rutaDb};Foreign Keys=True"));

        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<AuditoriaCobroTransaccional>();
        services.AddScoped<GestionCatalogo>();
        services.AddScoped<GestionInventario>();
        services.AddSingleton<IConsultaAislada, ConsultaAislada>();
        services.AddSingleton<ConsultasOperativas>();
        services.AddScoped<GestionVentas>();
        services.AddScoped<OperacionesPedido>();
        services.AddSingleton<IReembolsadorSimulado, ReembolsadorSimulado>();
        services.AddScoped<IGuiaEntrega, GuiaEntrega>();
        services.AddSingleton<IMetadatosCatalogo, CatalogoVisualFlyweightFactory>();
        services.AddScoped<IEmisorComprobantes, EmisorComprobantes>();
        services.AddScoped<IDemostracionOperativa, DemostracionOperativa>();

        // Repositorios
        services.AddScoped<IProductoRepository, ProductoRepository>();
        services.AddScoped<IKitRepository, KitRepository>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IPagoRepository, PagoRepository>();
        services.AddScoped<IDespachoRepository, DespachoRepository>();
        services.AddScoped<IDevolucionRepository, DevolucionRepository>();
        services.AddScoped<IMovimientoInventarioRepository, MovimientoInventarioRepository>();
        services.AddScoped<IEventoOutboxRepository, EventoOutboxRepository>();
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();

        // Patrón Singleton: Métricas compartidas en memoria
        services.AddSingleton<IMetricasSesion, MetricasSesion>();

        // Sesión de usuario actual
        services.AddSingleton<IServicioSesion, ServicioSesion>();
        services.AddSingleton<IVerificadorContrasena, VerificadorContrasena>();
        services.AddScoped<Autenticacion>();

        // Patrón Observer: Publicador de eventos de negocio
        services.AddScoped<IPublicadorEventos, PublicadorEventos>();
        services.AddScoped<IEventoNegocioObserver, MetricasPedidoObserver>();
        services.AddScoped<IEventoNegocioObserver, AuditoriaEventoObserver>();
        services.AddScoped<IEventoNegocioObserver, EventosOutboxObserver>();

        // Pasarelas de Pago (Adapter + Decorator + Proxy)
        services.AddSingleton<PasarelaModernaSimulada>();
        services.AddSingleton<PasarelaLegadaSimulada>();
        services.AddSingleton<PasarelaLegadaAdapter>();

        // Registrar IPasarelaPago:
        // Base: PasarelaModernaSimulada -> Envuelta por AuditoriaPagoDecorator -> Envuelta por PagoAutorizadoProxy
        services.AddScoped<ISelectorPasarelaPago>(sp => new SelectorPasarelaSimulada(
            ProtegerPasarela(sp, sp.GetRequiredService<PasarelaModernaSimulada>()),
            ProtegerPasarela(sp, sp.GetRequiredService<PasarelaLegadaAdapter>())));
        services.AddScoped<IPasarelaPago>(sp =>
            sp.GetRequiredService<ISelectorPasarelaPago>().Obtener("PasarelaModerna"));

        // Patrón Factory Method: Notificadores
        services.AddScoped<NotificadorCreator, EmailNotificadorCreator>();
        services.AddScoped<SmsNotificadorCreator>();

        // Outbox Processor
        services.AddScoped<OutboxProcessor>();
        services.AddSingleton<ConfiguracionEnvioOutbox>();
        services.AddScoped<IEnvioOutbox, EnvioOutboxSimulado>();
        services.AddSingleton<OutboxAutomatico>();
        services.AddSingleton<IOutboxProcessor>(sp => sp.GetRequiredService<OutboxAutomatico>());

        // Patrón Chain of Responsibility: Cadena de validación de pedidos
        services.AddScoped<IValidadorPedido>(sp =>
        {
            var clienteRepo = sp.GetRequiredService<IClienteRepository>();
            var prodRepo = sp.GetRequiredService<IProductoRepository>();
            var sesion = sp.GetRequiredService<IServicioSesion>();

            var val1 = new ClienteActivoValidador(clienteRepo);
            var val2 = new LineasPedidoValidador();
            var val3 = new StockDisponibleValidador(prodRepo);
            var val4 = new PermisoConfirmacionValidador(sesion);

            val1.SetSiguiente(val2)
                .SetSiguiente(val3)
                .SetSiguiente(val4);

            return val1;
        });

        // Patrón Facade: Confirmación de Pedido
        services.AddScoped<ConfirmacionPedidoFacade>();

        // Patrón Command: Invocador
        services.AddScoped<CommandInvoker>();

        // Patrón Mediator: Coordinador desacoplado entre formularios/vistas
        services.AddSingleton<IFormularioPrincipalMediator, FormularioPrincipalMediator>();

        // Vistas de interfaz de usuario
        services.AddTransient<AccesoView>();
        services.AddTransient<DashboardView>();
        services.AddTransient<CatalogoKitsView>();
        services.AddTransient<ClientesView>();
        services.AddTransient<CarritoBorradorView>();
        services.AddTransient<PedidosHistorialView>();
        services.AddTransient<CobroView>();
        services.AddTransient<DespachoView>();
        services.AddTransient<DevolucionesView>();
        services.AddTransient<InventarioView>();
        services.AddTransient<ReportesView>();
        services.AddTransient<AuditoriaPruebasView>();

        // Formulario Principal
        services.AddTransient<MainForm>();
    }
}
