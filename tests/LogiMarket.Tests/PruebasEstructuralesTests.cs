namespace LogiMarket.Tests;

using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Builder;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Facade;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Proxy;
using LogiMarket.Domain.Composite;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;
using LogiMarket.Infrastructure.Adapter;
using LogiMarket.Infrastructure.Bridge;
using LogiMarket.Infrastructure.Decorator;
using LogiMarket.Infrastructure.Flyweight;
using Xunit;

public class PruebasEstructuralesTests
{
    [Fact]
    public async Task P16_Adapter_ProveedorLegadoYAlternativo_MismoContratoSinModificarLegado()
    {
        // 1. Proveedor alternativo directo
        IPasarelaPago pasarelaModerna = new PasarelaModernaSimulada();
        var resMod = await pasarelaModerna.CobrarAsync(50.00m, "IDEM-MOD-1", "Test concepto");
        Assert.True(resMod.Exitoso);
        Assert.StartsWith("MOD-AUTH-", resMod.ReferenciaTransaccion);

        // 2. Proveedor legado adaptado sin alterar clase legada
        var legado = new PasarelaLegadaSimulada();
        IPasarelaPago pasarelaAdaptada = new PasarelaLegadaAdapter(legado);
        var resLeg = await pasarelaAdaptada.CobrarAsync(50.00m, "IDEM-LEG-1", "Test concepto");
        Assert.True(resLeg.Exitoso);
        Assert.StartsWith("LEGACY-AUTH-", resLeg.ReferenciaTransaccion);
        Assert.Equal(50.00m, resLeg.MontoCobrado);
    }

    [Fact]
    public void P17_Bridge_DosComprobantesPorDosMedios_CuatroCombinacionesFuncionales()
    {
        var pedido = new Pedido("PED-BR-01", 1, TipoEnvio.ESTANDAR);
        pedido.AgregarLinea(new PedidoDetalle("KIT_TELETRABAJO", "Kit Teletrabajo", true, 1, 60m));
        pedido.Cliente = new Cliente("C-VIP", "Ana", CategoriaCliente.VIP, "");
        pedido.CalcularTotales(5m);
        pedido.Validar();
        pedido.Pagar("REF-BR-123");

        var devolucion = new Devolucion(pedido.Id, MotivoDevolucion.DEFECTO, 54m, DateTime.UtcNow, "");

        var dirTxt = Path.Combine(Path.GetTempPath(), $"bridge_txt_{Guid.NewGuid():N}");

        try
        {
            // Combinación 1: ComprobanteVenta + VistaPreviaSalida
            var c1 = new ComprobanteVenta(new VistaPreviaSalida(), pedido);
            var r1 = c1.Emitir();
            Assert.Equal("VISTA_PREVIA", r1.TipoSalida);
            Assert.Contains("COMPROBANTE DE VENTA", r1.Contenido);
            Assert.Null(r1.RutaArchivo);

            // Combinación 2: ComprobanteVenta + ArchivoTxtSalida
            var c2 = new ComprobanteVenta(new ArchivoTxtSalida(dirTxt), pedido);
            var r2 = c2.Emitir();
            Assert.Equal("ARCHIVO_TXT", r2.TipoSalida);
            Assert.NotNull(r2.RutaArchivo);
            Assert.True(File.Exists(r2.RutaArchivo));

            // Combinación 3: ComprobanteDevolucion + VistaPreviaSalida
            var c3 = new ComprobanteDevolucion(new VistaPreviaSalida(), pedido, devolucion);
            var r3 = c3.Emitir();
            Assert.Equal("VISTA_PREVIA", r3.TipoSalida);
            Assert.Contains("COMPROBANTE DE DEVOLUCIÓN", r3.Contenido);
            Assert.True(r3.Contenido.Contains("54.00") || r3.Contenido.Contains("54,00"));

            // Combinación 4: ComprobanteDevolucion + ArchivoTxtSalida
            var c4 = new ComprobanteDevolucion(new ArchivoTxtSalida(dirTxt), pedido, devolucion);
            var r4 = c4.Emitir();
            Assert.Equal("ARCHIVO_TXT", r4.TipoSalida);
            Assert.NotNull(r4.RutaArchivo);
            Assert.True(File.Exists(r4.RutaArchivo));
            Assert.Contains("TOTAL REEMBOLSO", File.ReadAllText(r4.RutaArchivo));
        }
        finally
        {
            try { Directory.Delete(dirTxt, true); } catch { }
        }
    }

    [Fact]
    public void P18_Composite_KitsYComposicionAnidada_Precios50Y60_CicloRechazado()
    {
        var p01 = new ProductoSimple("P01", "Mouse USB", "Periféricos", "", 20.00m, 0.20m, 8);
        var p02 = new ProductoSimple("P02", "Teclado", "Periféricos", "", 30.00m, 0.70m, 5);
        var p03 = new ProductoSimple("P03", "Memoria USB", "Almacenamiento", "", 10.00m, 0.10m, 20);

        // KIT_OFICINA = P01 + P02 = USD 50.00
        var kitOficina = new KitCompuesto("KIT_OFICINA", "Kit Oficina");
        kitOficina.AgregarComponente(p01, 1);
        kitOficina.AgregarComponente(p02, 1);
        Assert.Equal(50.00m, kitOficina.ObtenerPrecio());

        // KIT_TELETRABAJO = KIT_OFICINA + P03 = USD 60.00
        var kitTeletrabajo = new KitCompuesto("KIT_TELETRABAJO", "Kit Teletrabajo");
        kitTeletrabajo.AgregarComponente(kitOficina, 1);
        kitTeletrabajo.AgregarComponente(p03, 1);
        Assert.Equal(60.00m, kitTeletrabajo.ObtenerPrecio());

        // Desglose terminal por SKU final
        var terminales = kitTeletrabajo.ObtenerDesgloseFisico();
        Assert.Equal(3, terminales.Count);
        Assert.Contains(terminales, t => t.Sku == "P01" && t.Cantidad == 1);
        Assert.Contains(terminales, t => t.Sku == "P02" && t.Cantidad == 1);
        Assert.Contains(terminales, t => t.Sku == "P03" && t.Cantidad == 1);

        // Intentar crear ciclo: Que KIT_OFICINA contenga a KIT_TELETRABAJO -> Debe lanzar CicloDetectadoException
        Assert.Throws<CicloDetectadoException>(() =>
        {
            kitOficina.AgregarComponente(kitTeletrabajo, 1);
        });

        // Intentar que un kit se contenga a sí mismo
        Assert.Throws<CicloDetectadoException>(() =>
        {
            kitOficina.AgregarComponente(kitOficina, 1);
        });
    }

    [Fact]
    public async Task P19_Decorator_AuditoriaPagoDecorator_RodeaEjecucionConLogsSinAlterarResultado()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var auditoriaRepo = scope.ServiceProvider.GetRequiredService<IAuditoriaRepository>();
        var sesion = scope.ServiceProvider.GetRequiredService<IServicioSesion>();
        var pasarela = new PasarelaModernaSimulada();

        var decorador = new AuditoriaPagoDecorator(pasarela, auditoriaRepo, sesion);

        // Cobro exitoso
        var resOk = await decorador.CobrarAsync(50m, "IDEM-DEC-1", "Concepto test");
        Assert.True(resOk.Exitoso);

        var auditorias = await auditoriaRepo.ObtenerTodosAsync();
        Assert.Contains(auditorias, a => a.Accion == "COBRO_INICIO" && a.Detalle.Contains("[INICIO]"));
        Assert.Contains(auditorias, a => a.Accion == "COBRO_EXITOSO" && a.Detalle.Contains("[FIN]"));

        // Cobro fallido
        pasarela.InducirError = true;
        var resFail = await decorador.CobrarAsync(50m, "IDEM-DEC-2", "Concepto test");
        Assert.False(resFail.Exitoso);

        var auditoriasPost = await auditoriaRepo.ObtenerTodosAsync();
        Assert.Contains(auditoriasPost, a => a.Accion == "COBRO_RECHAZADO" && a.Detalle.Contains("[FIN]"));
    }

    [Fact]
    public async Task P20_Facade_ConfirmarConFalloEnUnColaborador_ConsistenciaSinPersistenciaParcial()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
        var pagoRepo = scope.ServiceProvider.GetRequiredService<IPagoRepository>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IEventoOutboxRepository>();
        var pasarela = scope.ServiceProvider.GetRequiredService<PasarelaModernaSimulada>();

        var cliente = await clienteRepo.ObtenerPorCodigoAsync("C001");
        var pedido = new PedidoBuilder()
            .ConCodigo("PED-FACADE-FAIL")
            .ConCliente(cliente!)
            .AgregarItem(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8), 1)
            .Construir();

        await pedidoRepo.AgregarAsync(pedido);

        // Provocar fallo en la pasarela colaboradora
        pasarela.InducirError = true;

        var res = await facade.ConfirmarPedidoAsync(new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));
        Assert.False(res.Exitoso);

        // Verificar que ninguna persistencia parcial ocurrió (ni pago, ni outbox, ni descuento de stock)
        var p01 = await prodRepo.ObtenerPorSkuAsync("P01");
        Assert.Equal(8, p01!.Stock);

        var pago = await pagoRepo.ObtenerPorClaveIdempotenciaAsync(pedido.IdempotencyKey);
        Assert.Null(pago);

        var eventos = await outboxRepo.ObtenerTodosAsync();
        Assert.DoesNotContain(eventos, e => e.ContenidoJson.Contains("PED-FACADE-FAIL"));
    }

    [Fact]
    public async Task Facade_ConservaAuditoriaDeAutorizacionRevertidaAlFallarLaPersistencia()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var servicios = scope.ServiceProvider;
        var pedidos = servicios.GetRequiredService<IPedidoRepository>();
        var pagos = servicios.GetRequiredService<IPagoRepository>();
        var cliente = await servicios.GetRequiredService<IClienteRepository>().ObtenerPorCodigoAsync("C001");
        Pedido Crear(string codigo) => new PedidoBuilder().ConCodigo(codigo).ConCliente(cliente!)
            .AgregarItem(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8)).Construir();

        var existente = Crear("PED-AUD-PREVIO");
        var objetivo = Crear("PED-AUD-REVERTIDO");
        await pedidos.AgregarAsync(existente);
        await pedidos.AgregarAsync(objetivo);
        await pagos.AgregarAsync(new Pago(existente.Id, existente.Total, "REFERENCIA-EXISTENTE",
            existente.IdempotencyKey, "Prueba", ""));

        var resultado = await servicios.GetRequiredService<ConfirmacionPedidoFacade>()
            .ConfirmarPedidoAsync(new ConfirmarPedidoDto(objetivo.Id, existente.IdempotencyKey));
        Assert.False(resultado.Exitoso);
        var auditoria = await servicios.GetRequiredService<IAuditoriaRepository>().ObtenerTodosAsync();
        Assert.Single(auditoria, registro => registro.Accion == "COBRO_INICIO");
        Assert.Single(auditoria, registro => registro.Accion == "COBRO_AUTORIZACION_REVERTIDA" && !registro.Exitoso);
        Assert.DoesNotContain(auditoria, registro => registro.Accion == "COBRO_EXITOSO");
        Assert.Equal(8, (await servicios.GetRequiredService<IProductoRepository>().ObtenerPorSkuAsync("P01"))!.Stock);
        Assert.Equal(EstadoPedido.BORRADOR, (await pedidos.ObtenerPorIdAsync(objetivo.Id))!.Estado);
        Assert.Equal(existente.Id, (await pagos.ObtenerPorClaveIdempotenciaAsync(existente.IdempotencyKey))!.PedidoId);
    }

    [Fact]
    public void P21_Flyweight_1000FilasConMetadatosRepetidos_ReferenceEqualsTrueYMetadatosCompartidos()
    {
        var factory = new CatalogoVisualFlyweightFactory();
        var inicioMemoria = GC.GetTotalMemory(true);

        // Generar 1.000 filas de catálogo de vista
        var renglones = factory.GenerarRenglonesSimulados(1000);

        var finMemoria = GC.GetTotalMemory(false);
        var deltaMemoria = finMemoria - inicioMemoria;

        Assert.Equal(1000, renglones.Count);

        // En memoria solo deben existir 5 flyweights únicos
        Assert.Equal(5, factory.TotalFlyweightsInstanciados);

        // Verificar ReferenceEquals = true entre filas que apuntan al mismo SKU
        // Fila 0 es P01, Fila 5 es P01
        Assert.True(ReferenceEquals(renglones[0].MetadatosInmutables, renglones[5].MetadatosInmutables));
        Assert.Same(renglones[0].MetadatosInmutables, renglones[5].MetadatosInmutables);

        // Estado extrínseco es único por fila
        renglones[0].CantidadDeseada = 2;
        renglones[5].CantidadDeseada = 7;
        Assert.Equal(2, renglones[0].CantidadDeseada);
        Assert.Equal(7, renglones[5].CantidadDeseada);
    }

    [Fact]
    public async Task P22_Proxy_RolConsultaOBodegaIntentaPagar_RechazoAntesDeDecoratorYAdapter()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var auditoriaRepo = scope.ServiceProvider.GetRequiredService<IAuditoriaRepository>();
        var sesion = scope.ServiceProvider.GetRequiredService<IServicioSesion>();
        var pasarela = new PasarelaModernaSimulada();

        var decorador = new AuditoriaPagoDecorator(pasarela, auditoriaRepo, sesion);
        var proxy = new PagoAutorizadoProxy(decorador, sesion);

        // 1. Simular sesión con rol CONSULTA
        sesion.IniciarSesion(new Usuario("consulta.user", "hash", RolUsuario.CONSULTA));

        var auditoriasAntes = await auditoriaRepo.ObtenerTodosAsync();
        var conteoAntes = auditoriasAntes.Count;

        var resConsulta = await proxy.CobrarAsync(50m, "IDEM-CONSULTA", "Intento cobro");
        Assert.False(resConsulta.Exitoso);
        Assert.Contains("permisos de cobro", resConsulta.Mensaje, StringComparison.OrdinalIgnoreCase);

        // El Proxy cortó antes de Decorator y Adapter: no se generó ningún log [INICIO] ni [FIN] en el decorador
        var auditoriasDespues = await auditoriaRepo.ObtenerTodosAsync();
        Assert.Equal(conteoAntes, auditoriasDespues.Count);

        // 2. Simular sesión con rol BODEGA
        sesion.IniciarSesion(new Usuario("bodega.user", "hash", RolUsuario.BODEGA));
        var resBodega = await proxy.CobrarAsync(50m, "IDEM-BODEGA", "Intento cobro");
        Assert.False(resBodega.Exitoso);
        Assert.Contains("permisos de cobro", resBodega.Mensaje, StringComparison.OrdinalIgnoreCase);

        // 3. Simular sesión con rol VENTAS: autorizado
        sesion.IniciarSesion(new Usuario("ventas.user", "hash", RolUsuario.VENTAS));
        var resVentas = await proxy.CobrarAsync(50m, "IDEM-VENTAS", "Intento cobro");
        Assert.True(resVentas.Exitoso);

        // Ahora sí se generaron logs de auditoría por el Decorator
        var auditoriasVentas = await auditoriaRepo.ObtenerTodosAsync();
        Assert.True(auditoriasVentas.Count > conteoAntes);
    }
}
