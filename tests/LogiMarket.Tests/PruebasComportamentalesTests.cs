namespace LogiMarket.Tests;

using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Builder;
using LogiMarket.Application.Chain;
using LogiMarket.Application.Command;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Facade;
using LogiMarket.Application.Interpreter;
using LogiMarket.Application.Memento;
using LogiMarket.Application.Observer;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Composite;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;
using LogiMarket.Domain.Iterator;
using LogiMarket.Domain.State;
using LogiMarket.Domain.Strategy;
using LogiMarket.Domain.TemplateMethod;
using LogiMarket.Domain.Visitor;
using LogiMarket.Application.Mediator;
using Xunit;

public class PruebasComportamentalesTests
{
    private static Pedido PedidoParaFiltro(string codigo, EstadoPedido estado, decimal total)
    {
        var pedido = new Pedido(codigo, 1);
        pedido.AgregarLinea(new PedidoDetalle("P01", "Artículo", false, 1, total));
        pedido.CalcularTotales(0m);
        if (estado is EstadoPedido.VALIDADO or EstadoPedido.PAGADO or EstadoPedido.DESPACHADO or EstadoPedido.ENTREGADO)
            pedido.Validar();
        if (estado is EstadoPedido.PAGADO or EstadoPedido.DESPACHADO or EstadoPedido.ENTREGADO)
            pedido.Pagar("PAGO-FILTRO");
        if (estado is EstadoPedido.DESPACHADO or EstadoPedido.ENTREGADO)
            pedido.Despachar("GUIA-FILTRO");
        if (estado == EstadoPedido.ENTREGADO)
            pedido.Entregar();
        if (estado == EstadoPedido.CANCELADO)
            pedido.Cancelar("Prueba de filtro");
        return pedido;
    }

    [Fact]
    public async Task P23_Chain_PedidoSinClienteYSinStock_InformaPrimerErrorLuegoSiguiente()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var prodRepo = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
        var sesion = scope.ServiceProvider.GetRequiredService<IServicioSesion>();

        var val1 = new ClienteActivoValidador(clienteRepo);
        var val2 = new LineasPedidoValidador();
        var val3 = new StockDisponibleValidador(prodRepo);
        var val4 = new PermisoConfirmacionValidador(sesion);

        val1.SetSiguiente(val2).SetSiguiente(val3).SetSiguiente(val4);

        // 1. Pedido sin cliente y con stock insuficiente
        var pedido = new Pedido("PED-CHAIN-01", 999, TipoEnvio.ESTANDAR);
        pedido.AgregarLinea(new PedidoDetalle("P01", "Mouse", false, 100, 20m));
        pedido.AgregarComponenteFisico(new PedidoDetalleComponente("P01", "Mouse", 100, 20m, 0.2m));

        var res1 = await val1.ValidarAsync(pedido);
        Assert.False(res1.EsValido);
        Assert.Equal(nameof(ClienteActivoValidador), res1.PasoFallido);

        // 2. Corregir cliente (asignar cliente válido)
        var cliente = await clienteRepo.ObtenerPorCodigoAsync("C001");
        pedido.ConfigurarBorrador(cliente!.Id, pedido.TipoEnvio);

        // Ahora la cadena avanza hasta el eslabón de stock
        var res2 = await val1.ValidarAsync(pedido);
        Assert.False(res2.EsValido);
        Assert.Equal(nameof(StockDisponibleValidador), res2.PasoFallido);
        Assert.Contains("Stock insuficiente", res2.MensajeError);
    }

    [Fact]
    public async Task P24_Command_ConfirmarDesdeUiEInvocador_MismoComandoYResultadoConHistorial()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<ConfirmacionPedidoFacade>();
        var pedidoRepo = scope.ServiceProvider.GetRequiredService<IPedidoRepository>();
        var clienteRepo = scope.ServiceProvider.GetRequiredService<IClienteRepository>();
        var invoker = scope.ServiceProvider.GetRequiredService<CommandInvoker>();

        var cliente = await clienteRepo.ObtenerPorCodigoAsync("C001");
        var pedido = new PedidoBuilder()
            .ConCodigo("PED-CMD-01")
            .ConCliente(cliente!)
            .AgregarItem(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8), 1)
            .Construir();

        await pedidoRepo.AgregarAsync(pedido);

        var comando = new ConfirmarPedidoCommand(facade, new ConfirmarPedidoDto(pedido.Id, pedido.IdempotencyKey));

        // Ejecutar a través del invocador
        var res = await invoker.EjecutarAsync(comando);
        Assert.True(res.Exitoso);

        // Verificar registro de historial
        Assert.Single(invoker.Historial);
        var registro = invoker.Historial[0];
        Assert.Equal("ConfirmarPedidoCommand", registro.NombreComando);
        Assert.True(registro.Exitoso);
    }

    [Fact]
    public void P25_Interpreter_ConsultaEstadoPagadoAndTotalMayorOIgual50_FiltraCorrectamente()
    {
        var pedidos = new List<Pedido>
        {
            PedidoParaFiltro("PED-1", EstadoPedido.PAGADO, 59.00m),
            PedidoParaFiltro("PED-2", EstadoPedido.BORRADOR, 100.00m),
            PedidoParaFiltro("PED-3", EstadoPedido.PAGADO, 30.00m),
            PedidoParaFiltro("PED-4", EstadoPedido.PAGADO, 102.00m)
        };

        var query = "ESTADO = PAGADO AND TOTAL >= 50";
        var expresion = FiltroPedidosParser.Parse(query);

        var filtrados = pedidos.Where(p => expresion.Interpretar(p)).ToList();

        Assert.Equal(2, filtrados.Count);
        Assert.Contains(filtrados, p => p.Codigo == "PED-1");
        Assert.Contains(filtrados, p => p.Codigo == "PED-4");
    }

    [Fact]
    public void P26_Interpreter_SintaxisErroneaYParentesis_ErrorLegibleSinSqlYPrecedenciaCorrecta()
    {
        // 1. Sintaxis inválida -> Debe arrojar SintaxisFiltroInvalidaException
        Assert.Throws<SintaxisFiltroInvalidaException>(() =>
        {
            FiltroPedidosParser.Parse("ESTADO PAGADO SIN_OPERADOR");
        });

        // Paréntesis sin cerrar
        Assert.Throws<SintaxisFiltroInvalidaException>(() =>
        {
            FiltroPedidosParser.Parse("(ESTADO = PAGADO AND TOTAL >= 50");
        });

        // 2. Precedencia con paréntesis: (BORRADOR OR PAGADO) AND TOTAL > 50
        var expr = FiltroPedidosParser.Parse("(ESTADO = BORRADOR OR ESTADO = PAGADO) AND TOTAL > 50");

        var p1 = PedidoParaFiltro("P1", EstadoPedido.BORRADOR, 60m);
        var p2 = PedidoParaFiltro("P2", EstadoPedido.ENTREGADO, 60m);
        var p3 = PedidoParaFiltro("P3", EstadoPedido.PAGADO, 20m);

        Assert.True(expr.Interpretar(p1));
        Assert.False(expr.Interpretar(p2));
        Assert.False(expr.Interpretar(p3));
    }

    [Fact]
    public void P27_Iterator_RecorrerKitTeletrabajo_P01P02P03SinBucleInfinito()
    {
        var kitTeletrabajo = new KitCompuesto("KIT_TELETRABAJO", "Kit Teletrabajo");
        var kitOficina = new KitCompuesto("KIT_OFICINA", "Kit Oficina");
        kitOficina.AgregarComponente(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8), 1);
        kitOficina.AgregarComponente(new ProductoSimple("P02", "Teclado", "", "", 30m, 0.7m, 5), 1);
        kitTeletrabajo.AgregarComponente(kitOficina, 1);
        kitTeletrabajo.AgregarComponente(new ProductoSimple("P03", "Memoria", "", "", 10m, 0.1m, 20), 1);

        // Uso del iterador personalizado
        var iterator = new ComponenteFisicoIterator(kitTeletrabajo);
        var visitados = new List<ComponenteFisico>();

        while (iterator.MoveNext())
        {
            visitados.Add(iterator.Current);
        }

        Assert.Equal(3, visitados.Count);
        Assert.Equal("P01", visitados[0].Sku);
        Assert.Equal("P02", visitados[1].Sku);
        Assert.Equal("P03", visitados[2].Sku);
    }

    [Fact]
    public async Task P28_Mediator_ConfirmarYRevisarTresVistas_RefrescoSinAcoplamientoDirecto()
    {
        var mediator = new FormularioPrincipalMediator();
        bool vistaCatalogoRefrescada = false;
        bool vistaPedidosRefrescada = false;
        bool vistaMetricasRefrescada = false;

        var vCat = new TestVistaRecargable(() => vistaCatalogoRefrescada = true);
        var vPed = new TestVistaRecargable(() => vistaPedidosRefrescada = true);
        var vMet = new TestVistaRecargable(() => vistaMetricasRefrescada = true);

        mediator.RegistrarVista("Catalogo", vCat);
        mediator.RegistrarVista("Pedidos", vPed);
        mediator.RegistrarVista("Dashboard", vMet);

        // Operación de confirmación notificada a través del mediador
        await mediator.NotificarOperacionAsync("PEDIDO_CONFIRMADO");

        Assert.True(vistaCatalogoRefrescada);
        Assert.True(vistaPedidosRefrescada);
        Assert.True(vistaMetricasRefrescada);
    }

    [Fact]
    public void P29_Memento_TresEdiciones_DosDeshacer_UnRehacer_RestauraExacto()
    {
        var cliente = new Cliente("C001", "Ana", CategoriaCliente.VIP, "ana@test.com");
        var pedido = new Pedido("PED-MEM-01", 1, TipoEnvio.ESTANDAR) { Cliente = cliente };

        var caretaker = new HistorialBorradorCaretaker();

        // Estado 1: 1 Mouse
        caretaker.GuardarEstado(pedido);
        pedido.AgregarLinea(new PedidoDetalle("P01", "Mouse", false, 1, 20m));

        // Estado 2: Agregar 1 Teclado
        caretaker.GuardarEstado(pedido);
        pedido.AgregarLinea(new PedidoDetalle("P02", "Teclado", false, 1, 30m));

        // Estado 3: Agregar 1 Memoria USB
        caretaker.GuardarEstado(pedido);
        pedido.AgregarLinea(new PedidoDetalle("P03", "Memoria", false, 1, 10m));

        Assert.Equal(3, pedido.Lineas.Count);

        // 1er Deshacer -> Quedan 2 líneas
        caretaker.Deshacer(pedido);
        Assert.Equal(2, pedido.Lineas.Count);
        Assert.DoesNotContain(pedido.Lineas, l => l.ArticuloCodigo == "P03");

        // 2do Deshacer -> Queda 1 línea
        caretaker.Deshacer(pedido);
        Assert.Single(pedido.Lineas);
        Assert.Equal("P01", pedido.Lineas[0].ArticuloCodigo);

        // 1 Rehacer -> Vuelven a haber 2 líneas (P01 y P02)
        caretaker.Rehacer(pedido);
        Assert.Equal(2, pedido.Lineas.Count);
        Assert.Contains(pedido.Lineas, l => l.ArticuloCodigo == "P01");
        Assert.Contains(pedido.Lineas, l => l.ArticuloCodigo == "P02");
    }

    [Fact]
    public async Task P30_Observer_PedidoPagadoConTresSuscriptores_ActualizadosUnaSolaVez()
    {
        var publicador = new PublicadorEventos();
        int llamadasNotificacion = 0;
        int llamadasMetricas = 0;
        int llamadasAuditoria = 0;

        publicador.Suscribir(new TestObserver("Notificacion", () => llamadasNotificacion++));
        publicador.Suscribir(new TestObserver("Metricas", () => llamadasMetricas++));
        publicador.Suscribir(new TestObserver("Auditoria", () => llamadasAuditoria++));

        var evento = new EventoNegocio("PedidoPagado", 101, "PED-OBS-01", 59m, "Detalle", DateTime.UtcNow);
        await publicador.PublicarAsync(evento);

        Assert.Equal(1, llamadasNotificacion);
        Assert.Equal(1, llamadasMetricas);
        Assert.Equal(1, llamadasAuditoria);
    }

    [Fact]
    public void P31_State_CicloValido_Y_PagadoACanceladoInvalido()
    {
        var pedido = new Pedido("PED-STATE-01", 1);
        pedido.AgregarLinea(new PedidoDetalle("P01", "Mouse", false, 1, 20m));

        // BORRADOR -> VALIDADO
        Assert.Equal(EstadoPedido.BORRADOR, pedido.Estado);
        pedido.Validar();
        Assert.Equal(EstadoPedido.VALIDADO, pedido.Estado);

        // VALIDADO -> PAGADO
        pedido.Pagar("REF-123");
        Assert.Equal(EstadoPedido.PAGADO, pedido.Estado);

        // PAGADO -> CANCELADO es inválido: debe lanzar TransicionEstadoInvalidaException
        Assert.Throws<TransicionEstadoInvalidaException>(() =>
        {
            pedido.Cancelar("Motivo no permitido");
        });

        // PAGADO -> DESPACHADO
        pedido.Despachar("GUIA-001");
        Assert.Equal(EstadoPedido.DESPACHADO, pedido.Estado);

        // DESPACHADO -> ENTREGADO
        pedido.Entregar();
        Assert.Equal(EstadoPedido.ENTREGADO, pedido.Estado);
    }

    [Fact]
    public void P32_Strategy_VipMayoristaNormal_PrioridadSinAcumulacionYExtensible()
    {
        var calc = new CalculadoraDescuentoContext();

        // 1. Cliente VIP con 1 artículo -> Descuento 10%
        var pVip = new Pedido("P-VIP", 1)
        {
            Cliente = new Cliente("C1", "Ana", CategoriaCliente.VIP, ""),
        };
        pVip.AgregarLinea(new PedidoDetalle("P01", "Artículo", false, 1, 60m));
        pVip.CalcularTotales(0m);
        pVip.AgregarComponenteFisico(new PedidoDetalleComponente("P01", "Mouse", 1, 60m, 0.2m));
        var resVip = calc.DeterminarDescuento(pVip);
        Assert.Equal(6.00m, resVip.MontoDescuento);

        // 2. Cliente VIP pero con 10 unidades -> Gana MAYORISTA (15%) por prioridad!
        var pMayorista = new Pedido("P-MAY", 1)
        {
            Cliente = new Cliente("C1", "Ana", CategoriaCliente.VIP, ""),
        };
        pMayorista.AgregarLinea(new PedidoDetalle("P01", "Artículo", false, 1, 100m));
        pMayorista.CalcularTotales(0m);
        pMayorista.AgregarComponenteFisico(new PedidoDetalleComponente("P01", "Mouse", 10, 10m, 0.2m));
        var resMayorista = calc.DeterminarDescuento(pMayorista);
        Assert.Equal(15.00m, resMayorista.MontoDescuento);
        Assert.Contains("MAYORISTA", resMayorista.NombreEstrategia);

        // 3. Cliente NORMAL con <10 unidades -> Descuento 0%
        var pNormal = new Pedido("P-NORM", 1)
        {
            Cliente = new Cliente("C2", "Luis", CategoriaCliente.NORMAL, ""),
        };
        pNormal.AgregarLinea(new PedidoDetalle("P01", "Artículo", false, 1, 90m));
        pNormal.CalcularTotales(0m);
        pNormal.AgregarComponenteFisico(new PedidoDetalleComponente("P04", "Webcam", 4, 22.5m, 0.35m));
        var resNormal = calc.DeterminarDescuento(pNormal);
        Assert.Equal(0.00m, resNormal.MontoDescuento);
    }

    [Fact]
    public async Task P33_TemplateMethod_DefectoDia20Aceptado_ArrepentimientoDia10Rechazado()
    {
        var pedido = new Pedido("PED-DEV-01", 1);
        pedido.AgregarLinea(new PedidoDetalle("P01", "Producto", false, 1, 60m));
        pedido.Cliente = new Cliente("C-VIP", "Ana", CategoriaCliente.VIP, "");
        pedido.CalcularTotales(5m);
        pedido.Validar();
        pedido.Pagar("PAGO-DEV");
        pedido.Despachar("GUIA-DEV");
        pedido.Entregar();

        // 1. Defecto a día 20 (dentro de los 30 días) -> Aprobado
        var templateDefecto = new DevolucionPorDefecto();
        var solicitudDefecto = new SolicitudDevolucion(pedido, MotivoDevolucion.DEFECTO, pedido.FechaEntrega!.Value.AddDays(20), false, "Falla en botón");
        var pasos = new List<string>();
        Task RegistrarStock(ResultadoDevolucion _, CancellationToken __)
        {
            pasos.Add("stock");
            return Task.CompletedTask;
        }
        Task RegistrarEvento(ResultadoDevolucion _, CancellationToken __)
        {
            pasos.Add("evento");
            return Task.CompletedTask;
        }
        var resDefecto = await templateDefecto.ProcesarDevolucionAsync(solicitudDefecto, RegistrarStock, RegistrarEvento);
        Assert.Equal(new[] { "stock", "evento" }, pasos);

        Assert.True(resDefecto.Aprobada);
        Assert.Equal(54.00m, resDefecto.MontoReembolso); // Subtotal 60 - desc 6 = 54 (sin envío)

        // 2. Arrepentimiento a día 10 (límite 7 días) -> Rechazado
        var templateArrepentimiento = new DevolucionPorArrepentimiento();
        var solicitudArrepentimiento = new SolicitudDevolucion(pedido, MotivoDevolucion.ARREPENTIMIENTO, pedido.FechaEntrega!.Value.AddDays(10), true, "No lo necesito");
        pasos.Clear();
        var resArrepentimiento = await templateArrepentimiento.ProcesarDevolucionAsync(
            solicitudArrepentimiento, RegistrarStock, RegistrarEvento);
        Assert.Empty(pasos);

        Assert.False(resArrepentimiento.Aprobada);
        Assert.Contains("Plazo vencido", resArrepentimiento.MotivoRechazo);
        Assert.Equal(0m, resArrepentimiento.MontoReembolso);
    }

    [Fact]
    public void P34_Visitor_KitTeletrabajo_DesgloseP01P02P03YPesoTotal1Kg()
    {
        var kitTeletrabajo = new KitCompuesto("KIT_TELETRABAJO", "Kit Teletrabajo Completo");
        var kitOficina = new KitCompuesto("KIT_OFICINA", "Kit Oficina");
        kitOficina.AgregarComponente(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.20m, 8), 1);
        kitOficina.AgregarComponente(new ProductoSimple("P02", "Teclado", "", "", 30m, 0.70m, 5), 1);
        kitTeletrabajo.AgregarComponente(kitOficina, 1);
        kitTeletrabajo.AgregarComponente(new ProductoSimple("P03", "Memoria", "", "", 10m, 0.10m, 20), 1);

        // Visitor 1: Desglose de Componentes
        var visitorDesglose = new DesgloseComponentesVisitor();
        kitTeletrabajo.Aceptar(visitorDesglose);

        Assert.Equal(3, visitorDesglose.Desglose.Count);
        Assert.Equal(1, visitorDesglose.Desglose["P01"].CantidadTotal);
        Assert.Equal(1, visitorDesglose.Desglose["P02"].CantidadTotal);
        Assert.Equal(1, visitorDesglose.Desglose["P03"].CantidadTotal);

        // Visitor 2: Estimación de Peso Logístico: 0.20 + 0.70 + 0.10 = 1.00 kg
        var visitorPeso = new EstimacionPesoVisitor();
        kitTeletrabajo.Aceptar(visitorPeso);

        Assert.Equal(1.00m, visitorPeso.PesoTotalKg);
    }
}

public class TestVistaRecargable : IVistaRecargable
{
    private readonly Action _onRecargar;
    public TestVistaRecargable(Action onRecargar) => _onRecargar = onRecargar;
    public Task RecargarAsync() { _onRecargar(); return Task.CompletedTask; }
}

public class TestObserver : IEventoNegocioObserver
{
    private readonly Action _onActualizar;
    public string Nombre { get; }
    public TestObserver(string nombre, Action onActualizar)
    {
        Nombre = nombre;
        _onActualizar = onActualizar;
    }
    public Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        _onActualizar();
        return Task.CompletedTask;
    }
}
