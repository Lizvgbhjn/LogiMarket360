namespace LogiMarket.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Chain;
using LogiMarket.Application.Command;
using LogiMarket.Application.Facade;
using LogiMarket.Application.Observer;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Proxy;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Infrastructure.Adapter;
using LogiMarket.Infrastructure.Decorator;
using LogiMarket.Infrastructure.FactoryMethod;
using LogiMarket.Infrastructure.Outbox;
using LogiMarket.Infrastructure.Persistence;
using LogiMarket.Infrastructure.Session;
using LogiMarket.Application.Mediator;
using LogiMarket.Application.Ventas;
using LogiMarket.Infrastructure.AbstractFactory;

public class TestFixture : IDisposable
{
    public string DbPath { get; }
    public ServiceProvider ServiceProvider { get; }

    public TestFixture(RolUsuario rolInicial = RolUsuario.ADMIN)
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"test_lm_{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();

        // SQLite real aislado
        services.AddDbContext<LogiMarketDbContext>(options =>
            options.UseSqlite($"Data Source={DbPath}"));

        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<AuditoriaCobroTransaccional>();

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

        // Singleton & Sesión
        services.AddSingleton<IMetricasSesion, MetricasSesion>();
        services.AddSingleton<IServicioSesion, ServicioSesion>();
        services.AddSingleton<IPublicadorEventos, PublicadorEventos>();
        services.AddSingleton<IEventoNegocioObserver, MetricasPedidoObserver>();
        services.AddSingleton<IFormularioPrincipalMediator, FormularioPrincipalMediator>();

        // Pasarelas y decoradores
        services.AddSingleton<PasarelaModernaSimulada>();
        services.AddSingleton<PasarelaLegadaSimulada>();
        services.AddSingleton<PasarelaLegadaAdapter>();

        services.AddScoped<IPasarelaPago>(sp =>
        {
            var moderna = sp.GetRequiredService<PasarelaModernaSimulada>();
            var auditoriaRepo = sp.GetRequiredService<AuditoriaCobroTransaccional>();
            var sesion = sp.GetRequiredService<IServicioSesion>();
            var metricas = sp.GetRequiredService<IMetricasSesion>();

            var decorador = new AuditoriaPagoDecorator(moderna, auditoriaRepo, sesion);
            return new PagoAutorizadoProxy(decorador, sesion, metricas);
        });

        services.AddScoped<NotificadorCreator, EmailNotificadorCreator>();
        services.AddScoped<OutboxProcessor>();
        services.AddScoped<SmsNotificadorCreator>();
        services.AddSingleton<ConfiguracionEnvioOutbox>();
        services.AddScoped<IEnvioOutbox, EnvioOutboxSimulado>();
        services.AddSingleton<OutboxAutomatico>();
        services.AddSingleton<IOutboxProcessor>(sp => sp.GetRequiredService<OutboxAutomatico>());
        services.AddScoped<IGuiaEntrega, GuiaEntrega>();
        services.AddScoped<OperacionesPedido>();
        services.AddSingleton<IReembolsadorSimulado, ReembolsadorSimulado>();

        // Chain of Responsibility
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

        services.AddScoped<ConfirmacionPedidoFacade>();
        services.AddScoped<CommandInvoker>();

        ServiceProvider = services.BuildServiceProvider();

        // Inicializar esquema y seed
        using var scope = ServiceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        seeder.InicializarAsync().GetAwaiter().GetResult();

        // Autenticar usuario con el rol solicitado
        var sesion = scope.ServiceProvider.GetRequiredService<IServicioSesion>();
        var usuarioRepo = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();
        var user = usuarioRepo.ObtenerTodosAsync().GetAwaiter().GetResult().FirstOrDefault(u => u.Rol == rolInicial);
        if (user != null)
        {
            sesion.IniciarSesion(user);
        }
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
        try
        {
            if (File.Exists(DbPath))
            {
                File.Delete(DbPath);
            }
        }
        catch { }
    }
}
