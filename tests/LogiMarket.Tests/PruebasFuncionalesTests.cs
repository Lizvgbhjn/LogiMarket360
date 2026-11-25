namespace LogiMarket.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Builder;
using LogiMarket.Application.Chain;
using LogiMarket.Application.Command;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Facade;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Ventas;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Composite;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;
using LogiMarket.Infrastructure.Adapter;
using LogiMarket.Infrastructure.Persistence;
using LogiMarket.Infrastructure.Session;
using Xunit;

public class PruebasFuncionalesTests
{
    [Fact]
    public async Task P01_CreacionDeDbDesdeArchivoInexistenteYSeed_AplicaSinDuplicados()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
        var kitRepo = scope.ServiceProvider.GetRequiredService<IKitRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

        // Verificar carga inicial del seed
        var prods1 = await prodRepo.ObtenerTodosAsync();
        var kits1 = await kitRepo.ObtenerTodosAsync();
        var users1 = await userRepo.ObtenerTodosAsync();

        Assert.Contains(await scope.ServiceProvider.GetRequiredService<LogiMarketDbContext>().Database.GetAppliedMigrationsAsync(), id => id.EndsWith("_Inicial"));
        Assert.Equal(5, prods1.Count);
        Assert.Equal(2, kits1.Count);
        Assert.Equal(4, users1.Count);

        // Reejecutar semilla para verificar idempotencia (sin duplicados)
        await seeder.InicializarAsync();

        var prods2 = await prodRepo.ObtenerTodosAsync();
        var kits2 = await kitRepo.ObtenerTodosAsync();
        var users2 = await userRepo.ObtenerTodosAsync();

        Assert.Equal(5, prods2.Count);
        Assert.Equal(2, kits2.Count);
        Assert.Equal(4, users2.Count);
    }

    [Fact]
    public async Task P02_Ana_VIP_Compra_KitTeletrabajoX1_Estandar_Resultado59()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
        var pagoRepo = scope.ServiceProvider.GetRequiredService<IPagoRepository>();

        var ana = await clienteRepo.ObtenerPorCodigoAsync("C001");
        Assert.NotNull(ana);

        // Construir pedido mediante Builder
        var kitTeletrabajo = new KitCompuesto("KIT_TELETRABAJO", "Kit Teletrabajo Completo");
        var kitOficina = new KitCompuesto("KIT_OFICINA", "Kit Oficina");
        kitOficina.AgregarComponente(new ProductoSimple("P01", "Mouse USB", "Periféricos", "", 20.00m, 0.20m, 8), 1);
        kitOficina.AgregarComponente(new ProductoSimple("P02", "Teclado", "Periféricos", "", 30.00m, 0.70m, 5), 1);
        kitTeletrabajo.AgregarComponente(kitOficina, 1);
        kitTeletrabajo.AgregarComponente(new ProductoSimple("P03", "Memoria USB", "Almacenamiento", "", 10.00m, 0.10m, 20), 1);

        var pedido = new PedidoBuilder()
            .ConCodigo("PED-ANA-01")
            .ConCliente(ana)
            .ConTipoEnvio(TipoEnvio.ESTANDAR)
            .AgregarItem(kitTeletrabajo, 1)
            .Construir();

        // Subtotal 60.00 - Descuento VIP 10% (6.00) + Tarifa Estándar (5.00) = USD 59.00
        Assert.Equal(60.00m, pedido.Subtotal);
        Assert.Equal(6.00m, pedido.Descuento);
        Assert.Equal(5.00m, pedido.TarifaEnvio);
        Assert.Equal(59.00m, pedido.Total);

        await pedidoRepo.AgregarAsync(pedido);

        // Confirmar pedido
        var res = await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));
        Assert.True(res.Exitoso);

        // Verificar estado y existencias: P01 8->7, P02 5->4, P03 20->19
        var pedidoFinal = await pedidoRepo.ObtenerPorIdAsync(pedido.Id);
        Assert.NotNull(pedidoFinal);
        Assert.Equal(EstadoPedido.PAGADO, pedidoFinal.Estado);

        var p01 = await prodRepo.ObtenerPorSkuAsync("P01");
        var p02 = await prodRepo.ObtenerPorSkuAsync("P02");
        var p03 = await prodRepo.ObtenerPorSkuAsync("P03");

        Assert.Equal(7, p01!.Stock);
        Assert.Equal(4, p02!.Stock);
        Assert.Equal(19, p03!.Stock);

        var pago = await pagoRepo.ObtenerPorClaveIdempotenciaAsync(pedido.IdempotencyKey);
        Assert.NotNull(pago);
        Assert.Equal(59.00m, pago.Monto);
    }

    [Fact]
    public async Task P03_Luis_NORMAL_Compra_2P04_2P05_Expres_Resultado102()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();

        var luis = await clienteRepo.ObtenerPorCodigoAsync("C002");
        Assert.NotNull(luis);

        var p04 = new ProductoSimple("P04", "Webcam", "Video", "", 40.00m, 0.35m, 4);
        var p05 = new ProductoSimple("P05", "Alfombrilla", "Accesorios", "", 5.00m, 0.10m, 30);

        // 2*40 + 2*5 = 90.00. Normal desc = 0. Expres = 12.00. Total = 102.00
        var pedido = new PedidoBuilder()
            .ConCodigo("PED-LUIS-01")
            .ConCliente(luis)
            .ConTipoEnvio(TipoEnvio.EXPRES)
            .AgregarItem(p04, 2)
            .AgregarItem(p05, 2)
            .Construir();

        Assert.Equal(90.00m, pedido.Subtotal);
        Assert.Equal(0.00m, pedido.Descuento);
        Assert.Equal(12.00m, pedido.TarifaEnvio);
        Assert.Equal(102.00m, pedido.Total);

        await pedidoRepo.AgregarAsync(pedido);
        var res = await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));
        Assert.True(res.Exitoso);

        // P04 4->2, P05 30->28
        var p04Db = await prodRepo.ObtenerPorSkuAsync("P04");
        var p05Db = await prodRepo.ObtenerPorSkuAsync("P05");
        Assert.Equal(2, p04Db!.Stock);
        Assert.Equal(28, p05Db!.Stock);
    }

    [Fact]
    public async Task P04_Carmen_MAYORISTA_Compra_P03X10_Estandar_Resultado90()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();

        var carmen = await clienteRepo.ObtenerPorCodigoAsync("C003");
        var p03 = new ProductoSimple("P03", "Memoria USB", "Almacenamiento", "", 10.00m, 0.10m, 20);

        // 10 * 10 = 100.00. Unidades >= 10 -> MAYORISTA 15% (15.00). Estándar 5.00. Total = 90.00
        var pedido = new PedidoBuilder()
            .ConCodigo("PED-CARMEN-01")
            .ConCliente(carmen!)
            .ConTipoEnvio(TipoEnvio.ESTANDAR)
            .AgregarItem(p03, 10)
            .Construir();

        Assert.Equal(100.00m, pedido.Subtotal);
        Assert.Equal(15.00m, pedido.Descuento);
        Assert.Equal(5.00m, pedido.TarifaEnvio);
        Assert.Equal(90.00m, pedido.Total);

        await pedidoRepo.AgregarAsync(pedido);
        var res = await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));
        Assert.True(res.Exitoso);

        var p03Db = await prodRepo.ObtenerPorSkuAsync("P03");
        Assert.Equal(10, p03Db!.Stock);
    }

    [Fact]
    public async Task P05_KitTeletrabajoX6_Requiere6_P02_Y_SoloHay5_RechazoSinEfectosColaterales()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
        var pagoRepo = scope.ServiceProvider.GetRequiredService<IPagoRepository>();

        var cliente = await clienteRepo.ObtenerPorCodigoAsync("C001");

        var kitTeletrabajo = new KitCompuesto("KIT_TELETRABAJO", "Kit Teletrabajo");
        var kitOficina = new KitCompuesto("KIT_OFICINA", "Kit Oficina");
        kitOficina.AgregarComponente(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8), 1);
        kitOficina.AgregarComponente(new ProductoSimple("P02", "Teclado", "", "", 30m, 0.7m, 5), 1);
        kitTeletrabajo.AgregarComponente(kitOficina, 1);
        kitTeletrabajo.AgregarComponente(new ProductoSimple("P03", "Memoria", "", "", 10m, 0.1m, 20), 1);

        var pedido = new PedidoBuilder()
            .ConCodigo("PED-SOBREVENTA-01")
            .ConCliente(cliente!)
            .AgregarItem(kitTeletrabajo, 6) // Requiere 6 P02 y sólo existen 5
            .Construir();

        await pedidoRepo.AgregarAsync(pedido);

        var res = await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));

        // Debe ser rechazado por falta de stock de P02
        Assert.False(res.Exitoso);
        Assert.Contains("Stock insuficiente", res.Mensaje);

        // Sin efectos colaterales: stock de P02 sigue en 5, sin pago
        var p02 = await prodRepo.ObtenerPorSkuAsync("P02");
        Assert.Equal(5, p02!.Stock);

        var pago = await pagoRepo.ObtenerPorClaveIdempotenciaAsync(pedido.IdempotencyKey);
        Assert.Null(pago);

        var pedidoDb = await pedidoRepo.ObtenerPorIdAsync(pedido.Id);
        Assert.NotEqual(EstadoPedido.PAGADO, pedidoDb!.Estado);
    }

    [Fact]
    public async Task P06_SimularErrorDeCobroEnPedidoValido_ConservaInventarioYEstado()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
        var moderna = scope.ServiceProvider.GetRequiredService<PasarelaModernaSimulada>();

        var cliente = await clienteRepo.ObtenerPorCodigoAsync("C001");
        var prod = new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8);

        var pedido = new PedidoBuilder()
            .ConCodigo("PED-FALLO-COBRO-01")
            .ConCliente(cliente!)
            .AgregarItem(prod, 1)
            .Construir();

        await pedidoRepo.AgregarAsync(pedido);

        // Inducir fallo de cobro en pasarela
        moderna.InducirError = true;

        var res = await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));
        Assert.False(res.Exitoso);
        Assert.Contains("Cobro rechazado", res.Mensaje);

        var auditoria = scope.ServiceProvider.GetRequiredService<IAuditoriaRepository>();
        var logs = await auditoria.ObtenerTodosAsync();
        Assert.Single(logs, a => a.Accion == "COBRO_INICIO");
        Assert.Single(logs, a => a.Accion == "COBRO_RECHAZADO");

        // Inventario intacto (P01 sigue en 8)
        var p01 = await prodRepo.ObtenerPorSkuAsync("P01");
        Assert.Equal(8, p01!.Stock);

        var pedidoDb = await pedidoRepo.ObtenerPorIdAsync(pedido.Id);
        Assert.NotEqual(EstadoPedido.PAGADO, pedidoDb!.Estado);
    }

    [Fact]
    public async Task P07_RepetirConfirmacionConMismaClaveDeIdempotencia_NoDuplicaCobroNiStock()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
        var movRepo = scope.ServiceProvider.GetRequiredService<IMovimientoInventarioRepository>();

        var cliente = await clienteRepo.ObtenerPorCodigoAsync("C001");
        var prod = new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8);

        var pedido = new PedidoBuilder()
            .ConCodigo("PED-IDEMPOTENCIA-01")
            .ConCliente(cliente!)
            .AgregarItem(prod, 1)
            .Construir();

        await pedidoRepo.AgregarAsync(pedido);

        // Primera confirmación
        var res1 = await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));
        Assert.True(res1.Exitoso);

        var p01Post1 = await prodRepo.ObtenerPorSkuAsync("P01");
        Assert.Equal(7, p01Post1!.Stock);

        // Segunda confirmación con la misma clave de idempotencia
        var res2 = await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));
        Assert.True(res2.Exitoso);
        Assert.Contains("idempotente", res2.Mensaje, StringComparison.OrdinalIgnoreCase);

        // El stock NO se descontó dos veces (sigue en 7)
        var p01Post2 = await prodRepo.ObtenerPorSkuAsync("P01");
        Assert.Equal(7, p01Post2!.Stock);

        // Solo debe haber 1 movimiento de venta para este pedido
        var movs = await movRepo.ObtenerPorSkuAsync("P01");
        var movsPedido = movs.Where(m => m.Referencia == $"VENTA-{pedido.Codigo}").ToList();
        Assert.Single(movsPedido);
    }

    [Fact]
    public async Task P08_PedidoPAGADO_IntentarCancelarYModificar_OperacionesDenegadas()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var auditoriaRepo = scope.ServiceProvider.GetRequiredService<IAuditoriaRepository>();
        var sesion = scope.ServiceProvider.GetRequiredService<IServicioSesion>();

        var cliente = await clienteRepo.ObtenerPorCodigoAsync("C001");
        var pedido = new PedidoBuilder()
            .ConCodigo("PED-PAGADO-01")
            .ConCliente(cliente!)
            .AgregarItem(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8), 1)
            .Construir();

        await pedidoRepo.AgregarAsync(pedido);
        await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));

        // Pedido ahora está PAGADO. Intentar cancelar debe lanzar TransicionEstadoInvalidaException
        var cmdCancelar = new CancelarPedidoCommand(pedidoRepo, unitOfWork, auditoriaRepo, sesion, pedido.Id, "Intento cancelación");
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(() => cmdCancelar.EjecutarAsync());

        // Intentar modificar pedido pagado
        Assert.False(pedido.EstadoInterno.PuedeEditar);
    }

    [Fact]
    public async Task P09_DespacharYEntregarConBODEGA_IntentarDespacharSinPago_SecuenciaProtegida()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var despachoRepo = scope.ServiceProvider.GetRequiredService<IDespachoRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var auditoriaRepo = scope.ServiceProvider.GetRequiredService<IAuditoriaRepository>();
        var sesion = scope.ServiceProvider.GetRequiredService<IServicioSesion>();

        var cliente = await clienteRepo.ObtenerPorCodigoAsync("C001");
        var pedido = new PedidoBuilder()
            .ConCodigo("PED-LOGISTICA-01")
            .ConCliente(cliente!)
            .AgregarItem(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8), 1)
            .Construir();

        await pedidoRepo.AgregarAsync(pedido);

        // 1. Intentar despachar un pedido en BORRADOR sin haber pagado: debe fallar
        var cmdDespachoSinPago = new DespacharPedidoCommand(pedidoRepo, despachoRepo, unitOfWork, auditoriaRepo, sesion, new DespacharPedidoDto(pedido.Id, "GUIA-123"));
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(() => cmdDespachoSinPago.EjecutarAsync());

        // 2. Simular pago correcto
        pedido.Validar();
        pedido.Pagar("REF-PAGO-TEST");
        await pedidoRepo.ActualizarAsync(pedido);

        // Cambiar sesión a BODEGA
        var usuarioRepo = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();
        var bodegaUser = (await usuarioRepo.ObtenerTodosAsync()).First(u => u.Rol == RolUsuario.BODEGA);
        sesion.IniciarSesion(bodegaUser);

        // 3. Despachar con BODEGA en pedido PAGADO: debe tener éxito
        var cmdDespachoValido = new DespacharPedidoCommand(pedidoRepo, despachoRepo, unitOfWork, auditoriaRepo, sesion, new DespacharPedidoDto(pedido.Id, "GUIA-VALIDA-001"));
        var resDespacho = await cmdDespachoValido.EjecutarAsync();
        Assert.True(resDespacho.Exitoso);

        var pedidoDespachado = await pedidoRepo.ObtenerPorIdAsync(pedido.Id);
        Assert.Equal(EstadoPedido.DESPACHADO, pedidoDespachado!.Estado);

        var operaciones = scope.ServiceProvider.GetRequiredService<OperacionesPedido>();
        var ventas = (await usuarioRepo.ObtenerTodosAsync()).Single(u => u.Rol == RolUsuario.VENTAS);
        sesion.IniciarSesion(ventas);
        Assert.False((await operaciones.EntregarAsync(pedido.Id)).Exitoso);
        Assert.Equal("EN_TRANSITO", (await despachoRepo.ObtenerPorPedidoIdAsync(pedido.Id))!.EstadoDespacho);

        sesion.IniciarSesion(bodegaUser);
        Assert.True((await operaciones.EntregarAsync(pedido.Id)).Exitoso);
        Assert.False((await operaciones.EntregarAsync(pedido.Id)).Exitoso);

        using var lectura = fixture.ServiceProvider.CreateScope();
        var entregado = await lectura.ServiceProvider.GetRequiredService<IPedidoRepository>().ObtenerPorIdAsync(pedido.Id);
        var despacho = await lectura.ServiceProvider.GetRequiredService<IDespachoRepository>().ObtenerPorPedidoIdAsync(pedido.Id);
        Assert.Equal(EstadoPedido.ENTREGADO, entregado!.Estado);
        Assert.Equal("ENTREGADO", despacho!.EstadoDespacho);
        Assert.Equal(entregado.FechaEntrega, despacho.FechaEntregaReal);
        var eventos = await lectura.ServiceProvider.GetRequiredService<IEventoOutboxRepository>().ObtenerTodosAsync();
        Assert.Single(eventos, e => e.TipoEvento == "EntregaRegistrada" && e.ContenidoJson.Contains(pedido.Codigo));
    }

    [Fact]
    public async Task P10_CerrarYReabrir_InformacionPersisteEnSqliteYCoincide()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_p10_{Guid.NewGuid():N}.db");

        // Fase 1: Escribir pedido y confirmar en base de datos
        {
            var services = new ServiceCollection();
            services.AddDbContext<LogiMarketDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
            services.AddScoped<DatabaseSeeder>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<AuditoriaCobroTransaccional>();
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
            services.AddSingleton<IMetricasSesion, MetricasSesion>();
            services.AddSingleton<IServicioSesion, ServicioSesion>();
            services.AddSingleton<PasarelaModernaSimulada>();
            services.AddScoped<IPasarelaPago>(sp => sp.GetRequiredService<PasarelaModernaSimulada>());
            services.AddScoped<IValidadorPedido, LineasPedidoValidador>();
            services.AddScoped<ConfirmacionPedidoFacade>();

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.InicializarAsync();

            var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
            var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
            var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
            var sesion = scope.ServiceProvider.GetRequiredService<IServicioSesion>();
            sesion.IniciarSesion(new Usuario("admin", "hash", RolUsuario.ADMIN));

            var ana = await clienteRepo.ObtenerPorCodigoAsync("C001");
            var pedido = new PedidoBuilder()
                .ConCodigo("PED-PERSIST-01")
                .ConCliente(ana!)
                .AgregarItem(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8), 1)
                .Construir();

            await pedidoRepo.AgregarAsync(pedido);
            var res = await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));
            Assert.True(res.Exitoso);
        }

        // Fase 2: Simular reapertura de aplicación conectando a la misma base de datos
        {
            var services = new ServiceCollection();
            services.AddDbContext<LogiMarketDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
            services.AddScoped<IPedidoRepository, PedidoRepository>();
            services.AddScoped<IProductoRepository, ProductoRepository>();
            services.AddScoped<IPagoRepository, PagoRepository>();
            services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
            var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
            var pagoRepo = scope.ServiceProvider.GetRequiredService<IPagoRepository>();
            var auditoriaRepo = scope.ServiceProvider.GetRequiredService<IAuditoriaRepository>();

            var pedido = await pedidoRepo.ObtenerPorCodigoAsync("PED-PERSIST-01");
            Assert.NotNull(pedido);
            Assert.Equal(EstadoPedido.PAGADO, pedido.Estado);

            var p01 = await prodRepo.ObtenerPorSkuAsync("P01");
            Assert.Equal(7, p01!.Stock); // Descontado de 8 a 7

            var pago = await pagoRepo.ObtenerPorClaveIdempotenciaAsync(pedido.IdempotencyKey);
            Assert.NotNull(pago);

            var auditorias = await auditoriaRepo.ObtenerTodosAsync();
            Assert.Contains(auditorias, a => a.Accion == "CONFIRMAR_PEDIDO" && a.Detalle.Contains("PED-PERSIST-01"));
        }

        try { File.Delete(dbPath); } catch { }
    }
}
