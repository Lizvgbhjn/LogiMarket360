namespace LogiMarket.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using LogiMarket.Application.Builder;
using LogiMarket.Application.Command;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Facade;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Composite;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Infrastructure.Adapter;
using LogiMarket.Infrastructure.Bridge;
using LogiMarket.Infrastructure.Outbox;
using LogiMarket.Infrastructure.Persistence;
using LogiMarket.Infrastructure.FactoryMethod;
using Xunit;

public class PruebasIntegridadArquitecturaTests
{
    [Fact]
    public async Task SQLite_ImpideStockNegativoPrecioCeroYReferenciasSinPrincipal()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogiMarketDbContext>();
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_IntegridadCatalogo"));
        await using var con = new SqliteConnection($"Data Source={fixture.DbPath};Foreign Keys=True");
        await con.OpenAsync();
        var cambiosInvalidos = new[]
        {
            "UPDATE Productos SET Stock = -1 WHERE Sku = 'P01'",
            "UPDATE Productos SET Precio = '0.00' WHERE Sku = 'P01'",
            "UPDATE KitComponentes SET HijoCodigo = 'SIN-PRODUCTO' WHERE Id = (SELECT MIN(Id) FROM KitComponentes WHERE EsHijoKit = 0)",
            "UPDATE KitComponentes SET HijoCodigo = 'SIN-KIT' WHERE Id = (SELECT MIN(Id) FROM KitComponentes WHERE EsHijoKit = 1)",
            "DELETE FROM Productos WHERE Sku = 'P01'"
        };
        foreach (var sql in cambiosInvalidos)
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            var rechazo = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal(19, rechazo.SqliteErrorCode);
        }
    }

    [Fact]
    public async Task P35_DevolverPedidoEntregadoPorDefectoYRepetirSolicitud_Ana54ReembolsoYDevolucionUnica()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
        var devRepo = scope.ServiceProvider.GetRequiredService<IDevolucionRepository>();
        var movRepo = scope.ServiceProvider.GetRequiredService<IMovimientoInventarioRepository>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IEventoOutboxRepository>();
        var auditoriaRepo = scope.ServiceProvider.GetRequiredService<IAuditoriaRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var sesion = scope.ServiceProvider.GetRequiredService<IServicioSesion>();

        var ana = await clienteRepo.ObtenerPorCodigoAsync("C001");

        // Caso Ana: Subtotal 60.00, desc 6.00, flete 5.00, total 59.00
        var kitTeletrabajo = new KitCompuesto("KIT_TELETRABAJO", "Kit Teletrabajo Completo");
        var kitOficina = new KitCompuesto("KIT_OFICINA", "Kit Oficina");
        kitOficina.AgregarComponente(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8), 1);
        kitOficina.AgregarComponente(new ProductoSimple("P02", "Teclado", "", "", 30m, 0.7m, 5), 1);
        kitTeletrabajo.AgregarComponente(kitOficina, 1);
        kitTeletrabajo.AgregarComponente(new ProductoSimple("P03", "Memoria", "", "", 10m, 0.1m, 20), 1);

        var pedido = new PedidoBuilder()
            .ConCodigo("PED-DEV-ANA")
            .ConCliente(ana!)
            .AgregarItem(kitTeletrabajo, 1)
            .Construir();

        await pedidoRepo.AgregarAsync(pedido);
        await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));

        // Despachar y Entregar
        pedido.Despachar("GUIA-DEV-001");
        pedido.Entregar();
        await pedidoRepo.ActualizarAsync(pedido);

        // 1. Primera solicitud de devolución por DEFECTO
        var cmd1 = new ProcesarDevolucionCommand(
            pedidoRepo, prodRepo, devRepo, movRepo, outboxRepo, auditoriaRepo, unitOfWork, sesion,
            new SolicitarDevolucionDto(pedido.Id, MotivoDevolucion.DEFECTO, false, "Falla en periférico"), new ReembolsadorSimulado());

        var res1 = await cmd1.EjecutarAsync();
        Assert.True(res1.Exitoso);

        // Reembolso debe ser exactamente 54.00 (sin la tarifa de envío de 5.00)
        var dev = await devRepo.ObtenerPorPedidoIdAsync(pedido.Id);
        Assert.NotNull(dev);
        Assert.Equal(54.00m, dev.MontoReembolsado);
        var evento = Assert.Single(await outboxRepo.ObtenerTodosAsync(), e => e.TipoEvento == "DevolucionAceptada");
        Assert.Contains($"REEMBOLSO-SIM-{pedido.Id}", evento.ContenidoJson);

        // Stock reintegrado: P01 7->8, P02 4->5, P03 19->20
        var p01 = await prodRepo.ObtenerPorSkuAsync("P01");
        var p02 = await prodRepo.ObtenerPorSkuAsync("P02");
        var p03 = await prodRepo.ObtenerPorSkuAsync("P03");
        Assert.Equal(8, p01!.Stock);
        Assert.Equal(5, p02!.Stock);
        Assert.Equal(20, p03!.Stock);

        // 2. Repetir la solicitud sobre el mismo pedido ya devuelto -> Debe ser RECHAZADA (devolución única)
        var cmd2 = new ProcesarDevolucionCommand(
            pedidoRepo, prodRepo, devRepo, movRepo, outboxRepo, auditoriaRepo, unitOfWork, sesion,
            new SolicitarDevolucionDto(pedido.Id, MotivoDevolucion.DEFECTO, false, "Segundo intento"), new ReembolsadorSimulado());

        var res2 = await cmd2.EjecutarAsync();
        Assert.False(res2.Exitoso);
        Assert.Contains("ya cuenta con una devolución", res2.Mensaje);

        // Stock NO se vuelve a reintegrar
        var p01Post = await prodRepo.ObtenerPorSkuAsync("P01");
        Assert.Equal(8, p01Post!.Stock);
    }

    [Fact]
    public async Task P36_DosCobrosConcurrentesSobreUnSkuConStockUno_SinSobreventaYStockNoNegativo()
    {
        using var fixture = new TestFixture();

        // 1. Configurar P04 con exactamente 1 unidad de stock
        using (var setupScope = fixture.ServiceProvider.CreateScope())
        {
            var prodRepo = setupScope.ServiceProvider.GetRequiredService<IProductoRepository>();
            var clienteRepo = setupScope.ServiceProvider.GetRequiredService<IClienteRepository>();
            var pedidoRepo = setupScope.ServiceProvider.GetRequiredService<IPedidoRepository>();

            var p04 = await prodRepo.ObtenerPorSkuAsync("P04");
            p04!.DescontarStock(3); // Tenía 4, ahora tiene 1
            await prodRepo.ActualizarAsync(p04);
            Assert.Equal(1, p04.Stock);

            var cliente = await clienteRepo.ObtenerPorCodigoAsync("C001");

            var ped1 = new PedidoBuilder()
                .ConCodigo("PED-CONC-1")
                .ConCliente(cliente!)
                .AgregarItem(new ProductoSimple("P04", "Webcam", "", "", 40m, 0.35m, 1), 1)
                .Construir();

            var ped2 = new PedidoBuilder()
                .ConCodigo("PED-CONC-2")
                .ConCliente(cliente!)
                .AgregarItem(new ProductoSimple("P04", "Webcam", "", "", 40m, 0.35m, 1), 1)
                .Construir();

            await pedidoRepo.AgregarAsync(ped1);
            await pedidoRepo.AgregarAsync(ped2);
        }

        // 2. Ejecutar confirmaciones concurrentes usando scopes independientes
        var task1 = Task.Run(async () =>
        {
            using var scope1 = fixture.ServiceProvider.CreateScope();
            var facade1 = scope1.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
            var pedRepo1 = scope1.ServiceProvider.GetRequiredService<IPedidoRepository>();
            var p1 = await pedRepo1.ObtenerPorCodigoAsync("PED-CONC-1");
            return await facade1.ConfirmarPedidoAsync(new ConfirmarPedidoDto(p1!.Id, p1.IdempotencyKey));
        });

        var task2 = Task.Run(async () =>
        {
            using var scope2 = fixture.ServiceProvider.CreateScope();
            var facade2 = scope2.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
            var pedRepo2 = scope2.ServiceProvider.GetRequiredService<IPedidoRepository>();
            var p2 = await pedRepo2.ObtenerPorCodigoAsync("PED-CONC-2");
            return await facade2.ConfirmarPedidoAsync(new ConfirmarPedidoDto(p2!.Id, p2.IdempotencyKey));
        });

        var resultados = await Task.WhenAll(task1, task2);

        int aprobados = resultados.Count(r => r.Exitoso);
        int rechazados = resultados.Count(r => !r.Exitoso);

        Assert.Equal(1, aprobados);
        Assert.Equal(1, rechazados);

        using (var verifyScope = fixture.ServiceProvider.CreateScope())
        {
            var prodRepo = verifyScope.ServiceProvider.GetRequiredService<IProductoRepository>();
            var p04Final = await prodRepo.ObtenerPorSkuAsync("P04");
            Assert.Equal(0, p04Final!.Stock);
            Assert.True(p04Final.Stock >= 0);
        }
    }

    [Fact]
    public async Task P37_Outbox_FallaNotificadorYReiniciarProcesador_RecuperaYProcesaUnaSolaVez()
    {
        using var fixture = new TestFixture();
        int eventoId;
        using (var primerScope = fixture.ServiceProvider.CreateScope())
        {
            var sp = primerScope.ServiceProvider;
            var eventos = sp.GetRequiredService<IEventoOutboxRepository>();
            var pedidos = sp.GetRequiredService<IPedidoRepository>();
            var cliente = await sp.GetRequiredService<IClienteRepository>().ObtenerPorCodigoAsync("C001");
            var pedido = new PedidoBuilder().ConCodigo("PED-OUT-01").ConCliente(cliente!)
                .AgregarItem(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8)).Construir();
            await pedidos.AgregarAsync(pedido);
            var evento = new EventoOutbox
            {
                TipoEvento = "PedidoPagado",
                ContenidoJson = System.Text.Json.JsonSerializer.Serialize(new { PedidoId = pedido.Id })
            };
            await eventos.AgregarAsync(evento);
            eventoId = evento.Id;

            var notificador = new EnvioOutboxSimulado(pedidos, new CreadorNoDisponible(),
                sp.GetRequiredService<SmsNotificadorCreator>(), new ConfiguracionEnvioOutbox());
            var procesador = new OutboxProcessor(eventos, notificador, sp.GetRequiredService<IAuditoriaRepository>());
            Assert.Equal(0, await procesador.ProcesarPendientesAsync());
            var fallo = Assert.Single(await eventos.ObtenerTodosAsync());
            Assert.Equal(EstadoOutbox.FALLIDO, fallo.Estado);
            Assert.Equal("Notificador no disponible", fallo.UltimoError);
            Assert.Equal(1, fallo.Reintentos);
        }

        // La tarea periódica usa este mismo ciclo y crea su propio scope de SQLite.
        var automatico = fixture.ServiceProvider.GetRequiredService<OutboxAutomatico>();
        Assert.Equal(1, await automatico.EjecutarCicloAsync(true));
        Assert.Equal(0, await automatico.EjecutarCicloAsync(true));

        using var lectura = fixture.ServiceProvider.CreateScope();
        var final = Assert.Single(await lectura.ServiceProvider.GetRequiredService<IEventoOutboxRepository>().ObtenerTodosAsync());
        Assert.Equal(eventoId, final.Id);
        Assert.Equal(EstadoOutbox.PROCESADO, final.Estado);
        Assert.NotNull(final.FechaProcesamiento);
        Assert.Null(final.UltimoError);
        var registros = await lectura.ServiceProvider.GetRequiredService<IAuditoriaRepository>().ObtenerTodosAsync();
        var aviso = Assert.Single(registros, a => a.Accion == "AVISO_EMAIL");
        Assert.Contains("ana@ejemplo.test", aviso.Detalle);
        Assert.Single(registros, a => a.Accion == "OUTBOX_PROCESADO");
    }

    private sealed class CreadorNoDisponible : NotificadorCreator
    {
        public override INotificador CrearNotificador() => new NotificadorNoDisponible();
    }

    private sealed class NotificadorNoDisponible : INotificador
    {
        public string Canal => "EMAIL";

        public Task<ResultadoNotificacion> EnviarAsync(string destinatario, string mensaje, CancellationToken ct = default) =>
            Task.FromException<ResultadoNotificacion>(new InvalidOperationException("Notificador no disponible"));
    }

    [Fact]
    public async Task P38_PruebasDeLspIspDip_Y_ArquitecturaLimpia()
    {
        // 1. LSP (Liskov Substitution Principle): Dos implementaciones de IPasarelaPago son intercambiables
        IPasarelaPago prov1 = new PasarelaModernaSimulada();
        IPasarelaPago prov2 = new PasarelaLegadaAdapter(new PasarelaLegadaSimulada());

        Assert.Equal(typeof(string), prov1.NombreProveedor.GetType());
        Assert.Equal(typeof(string), prov2.NombreProveedor.GetType());

        var res1 = await prov1.CobrarAsync(10m, "KEY1", "Test");
        var res2 = await prov2.CobrarAsync(10m, "KEY2", "Test");

        Assert.True(res1.Exitoso);
        Assert.True(res2.Exitoso);
        foreach (var resultado in new[] { res1, res2 })
        {
            Assert.Equal(10m, resultado.MontoCobrado);
            Assert.Matches("^(MOD|LEGACY)-AUTH-[0-9A-F]{32}$", resultado.ReferenciaTransaccion);
        }
        Assert.NotEqual(res1.ReferenciaTransaccion, res2.ReferenciaTransaccion);
        foreach (var pasarela in new[] { prov1, prov2 })
        {
            foreach (var (monto, clave) in new[] { (0m, "OK"), (-1m, "OK"), (0.001m, "OK"), (decimal.MaxValue, "OK"), (10m, ""), (10m, " ") })
            {
                var rechazo = await pasarela.CobrarAsync(monto, clave, "Entrada inválida");
                Assert.False(rechazo.Exitoso);
                Assert.Equal(0m, rechazo.MontoCobrado);
                Assert.Empty(rechazo.ReferenciaTransaccion);
            }

            using var cancelado = new CancellationTokenSource();
            cancelado.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pasarela.CobrarAsync(10m, "CANCELADA", "Cancelación", cancelado.Token));
        }

        static string[] ProyectosPropios(Type tipo) => tipo.Assembly.GetReferencedAssemblies()
            .Select(ensamblado => ensamblado.Name!)
            .Where(nombre => nombre.StartsWith("LogiMarket.", StringComparison.Ordinal))
            .OrderBy(nombre => nombre, StringComparer.Ordinal).ToArray();
        Assert.Empty(ProyectosPropios(typeof(Pedido)));
        Assert.Equal(new[] { "LogiMarket.Domain" }, ProyectosPropios(typeof(ConfirmacionPedidoFacade)));
        Assert.Equal(new[] { "LogiMarket.Application", "LogiMarket.Domain" }, ProyectosPropios(typeof(LogiMarketDbContext)));

        // 2. ISP (Interface Segregation Principle): Contratos pequeños y específicos
        // ISalidaDocumento contiene una única operación de emisión sin métodos adicionales innecesarios
        var metodosOperacionales = typeof(ISalidaDocumento)
            .GetMethods()
            .Where(m => !m.IsSpecialName)
            .ToList();
        Assert.Single(metodosOperacionales);
        Assert.Equal("Imprimir", metodosOperacionales[0].Name);

        // 3. DIP (Dependency Inversion Principle):
        // ConfirmacionPedidoFacade depende de interfaces abstractas, no de clases concretas
        var constructorParams = typeof(ConfirmacionPedidoFacade).GetConstructors()[0].GetParameters();
        Assert.All(constructorParams, p =>
        {
            Assert.True(p.ParameterType.IsInterface, $"El parámetro '{p.Name}' en ConfirmacionPedidoFacade debe ser una interfaz.");
        });
    }
}
